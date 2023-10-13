using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.WindowsRuntime.AsyncInfo;

namespace JacobC.Xiami
{
    /// <summary>
    /// Load an incremental collection of items by page
    /// </summary>
    /// <typeparam name="T">Collection item type</typeparam>
    public class PageItemsCollection<T> : IncrementalLoadingBase<T>
    {
        public delegate Task<IEnumerable<T>> FetchPageDelegate(
            uint pageIndex, CancellationToken token);

        FetchPageDelegate _fetchPage;
        bool _hasMore = true;
        uint _current = 1;//Current page number

        public uint PageCapacity { get; set; }

        /// <summary>
        /// Get one based on the existing complete first page 
        /// content <see cref="PageItemsCollection{T}"/> object
        /// </summary>
        /// <param name="firstpage">For the complete content of the first page, 
        /// please make sure that the number after that is the same as the number of pages</param>
        /// <param name="fetchPage">How to get a page</param>
        public PageItemsCollection(IEnumerable<T> firstpage, FetchPageDelegate fetchPage)
            : base(firstpage)
        {
            //AddRange(firstpage); //Already in base(firstpage in implementation)
            PageCapacity = (uint)Count;
            _fetchPage = fetchPage;
        }

        /// <summary>
        /// Specify the number of items per page to get 
        /// one <see cref="PageItemsCollection{T}"/> object, and get the first page
        /// </summary>
        /// <param name="capacity">Number of items per page</param>
        /// <param name="fetchPage">How to get a page</param>
        public PageItemsCollection(uint capacity, FetchPageDelegate fetchPage) : base()
        {
            PageCapacity = capacity;
            _fetchPage = fetchPage;
            _GetFirst();
        }
        /// <summary>
        /// Specify the number of items per page to get 
        /// one <see cref="PageItemsCollection{T}"/> object, 
        /// and write part of the content of the first page
        /// </summary>
        /// <param name="capacity">Number of items per page</param>
        /// <param name="firstpart">Part of the first page</param>
        /// <param name="fetchpage">How to get a page</param>
        public PageItemsCollection(uint capacity, 
            IEnumerable<T> firstpart, FetchPageDelegate fetchpage) : base(firstpart)
        {
            //AddRange(firstpart);
            PageCapacity = capacity;
            _fetchPage = fetchpage;
        }
        /// <summary>
        /// Get a fixed size <see cref="PageItemsCollection{T}"/>
        /// </summary>
        /// <param name="items"></param>
        public PageItemsCollection(IEnumerable<T> items):base(items)
        {
            //AddRange(items);
            PageCapacity = (uint)Count;
            _hasMore = false;
            _fetchPage = (a, b) => null;
        }
        internal async void _GetFirst()
        {
            var result = await Run((c) => _fetchPage?.Invoke(1, c));
            if (result == null)
                _hasMore = false;
            else if (result.Count() < PageCapacity)
                _hasMore = false;
            else
                _hasMore = true;
            AddRange(result);
        }

        protected sealed override bool HasMoreItemsOverride()
        {
            return _hasMore;
        }

        protected override async Task<IEnumerable<T>> LoadMoreItemsAsync(
            CancellationToken c, uint count)
        {
            var returnlist = new List<T>();
            if (Count < PageCapacity)
            {
                //For situations where the first page is not full
                var r = await _fetchPage(1, c);
                var tc = Count;
                var sc = Count;
                foreach (var item in r)
                {
                    if (tc > 0)
                    {
                        tc--;
                        continue;
                    }
                    returnlist.Add(item);
                    sc++;
                    if (count > 0) count--;
                }
                if (sc < PageCapacity)
                {
                    _hasMore = false;
                    return returnlist;
                }
            }
            uint pages = count / PageCapacity + _current;
            if (count % PageCapacity != 0) pages++;
            for (uint i = _current + 1; i <= pages && _hasMore; i++)
            {
                var r = await _fetchPage(i, c);
                // In order to ensure order, concurrency is not used.
                // TODO: Consider ordering after concurrency?
                
                //if (i == pages)
                if (r == null)
                    _hasMore = false;
                else
                {
                    returnlist.AddRange(r);
                    if (r.Count() < PageCapacity)
                        _hasMore = false;
                    else
                    {
                        _hasMore = true;
                        _current = i;
                    }
                }
            }
            return returnlist;
        }
    }
}
