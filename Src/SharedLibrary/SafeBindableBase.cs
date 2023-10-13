using JacobC.Xiami.Services;
using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Template10.Mvvm;

namespace JacobC.Xiami
{
    /// <summary>
    /// Guarantee that attribute changes are enforced<see cref="BindableBase"/>
    /// </summary>
    public class SafeBindableBase : BindableBase
    {
        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            try
            {
                // when WindowWrapper.Dispatcher will be thrown when it does not exist NullReferenceException
                base.RaisePropertyChanged(propertyName);
            }
            catch (NullReferenceException ne)
            {
                LogService.ErrorWrite(ne, "BindableBase");
            }
            catch
            {
                throw;
            }
        }
        public override void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpression)
        {
            try
            {
                // when WindowWrapper.Dispatcher will be thrown when it does not exist NullReferenceException
                base.RaisePropertyChanged<T>(propertyExpression);
            }
            catch (NullReferenceException ne)
            {
                LogService.ErrorWrite(ne, "BindableBase");
            }
            catch
            {
                throw;
            }
        }

        public override bool Set<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            bool result = false;
            try
            {
                result = base.Set<T>(ref storage, value, propertyName);
            }
            catch
            {
                throw;
            }
            finally
            {
                if(!result) storage = value;
            }
            return result;
        }
        public override bool Set<T>(Expression<Func<T>> propertyExpression, ref T field, T newValue)
        {
            bool result = false;
            try
            {
                result = base.Set<T>(propertyExpression, ref field, newValue);
            }
            catch
            {
                throw;
            }
            finally
            {
                field = newValue;
            }
            return result;
        }
    }
}
