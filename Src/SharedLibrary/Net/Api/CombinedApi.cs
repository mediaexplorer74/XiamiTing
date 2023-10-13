using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JacobC.Xiami.Models;
using Windows.Foundation;
using static System.Runtime.InteropServices.WindowsRuntime.AsyncInfo;

namespace JacobC.Xiami.Net
{
    public class CombinedApi : IXiamiApi
    {
        private CombinedApi() { }
        static CombinedApi _instance;
        /// <summary>
        /// get only example of <see cref="CombinedApi"/> 
        /// </summary>
        public static CombinedApi Instance
        {
            get
            {
                if (_instance == null) _instance = new CombinedApi();
                return _instance;
            }
        }

        // Album information is more complicated, only consider the Web
        public IAsyncAction GetAlbumInfo(AlbumModel album, bool cover = false)
        {
            return Run(async (c) =>
            {
                await WebApi.Instance.GetAlbumInfo(album, cover);
            });
        }

        public IAsyncAction GetArtistInfo(ArtistModel artist, bool cover = false)
        {
            throw new NotImplementedException();
        }

        //TODO: Compare the acquired song information to reduce repeated acquisition expenses,
        //especially several IEnumerable.You can consider that WebAPI cover is true
        public IAsyncAction GetSongInfo(SongModel song, bool cover = false)
        {
            return Run(async (c) =>
            {
                await WapApi.Instance.GetSongInfo(song, cover);
                await WebApi.Instance.GetSongInfo(song, cover);
            });
        }
    }
}
