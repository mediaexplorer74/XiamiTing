﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Services.SettingsService;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using Windows.Foundation;

namespace JacobC.Xiami
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// 读取设置后删除该设置
        /// </summary>
        public static T ReadAndReset<T>(this ISettingsService setting, string key, T otherwise = default(T))
        {
            T val = setting.Read<T>(key, otherwise);
            setting.Remove(key);
            return val;
        }
        /// <summary>
        /// 将对象转换成枚举类型
        /// </summary>
        public static T ParseEnum<T>(object value) => (T)(Enum.Parse(typeof(T), value.ToString()));
        /// <summary>
        /// 将<see cref="DependencyPropertyChangedEventArgs"/>类型转换成<see cref="ChangedEventArgs{TValue}"类型/>
        /// </summary>
        /// <typeparam name="T">ChangedEventArgs参数类型</typeparam>
        public static ChangedEventArgs<T> ToChangedEventArgs<T>(this DependencyPropertyChangedEventArgs e)
            => new ChangedEventArgs<T>((T)e.OldValue, (T)e.NewValue);
        /// <summary>
        /// 使async方法同步运行
        /// </summary>
        /// <param name="asyncMethod">要同步运行的async方法</param>
        public static void InvokeAndWait(Func<Task> asyncMethod)
        {
            Task.Run(() => asyncMethod())
                .ContinueWith(task => task.Wait())
                .Wait();
        }
        /// <summary>
        /// 使返回<see cref="IAsyncAction"/>的无参方法同步运行
        /// </summary>
        /// <param name="asyncMethod">要同步运行的方法</param>
        public static void InvokeAndWait(Func<IAsyncAction> asyncMethod) => InvokeAndWait(async () => await asyncMethod());
        /// <summary>
        /// 使async方法同步运行
        /// </summary>
        /// <param name="asyncMethod">要同步运行的async方法</param>
        public static T InvokeAndWait<T>(Func<Task<T>> asyncMethod)
        {
            Task<T> t = Task.Run(() => asyncMethod())
                .ContinueWith(task =>
                {
                    task.Wait();
                    return task.Result;
                });
            t.Wait();
            return t.Result;
        }
        /// <summary>
        /// 使返回<see cref="IAsyncOperation{TResult}"/>的无参方法同步运行
        /// </summary>
        /// <param name="asyncMethod">要同步运行的方法</param>
        public static T InvokeAndWait<T>(Func<IAsyncOperation<T>> asyncMethod) => InvokeAndWait(async () => await asyncMethod());
        /// <summary>
        /// 获取指定类型的参数
        /// </summary>
        /// <typeparam name="T">需要获取的参数类型</typeparam>
        public static T GetParameter<T>(this NavigationEventArgs e)
        {
            return Template10.Services.SerializationService.SerializationService.Json.Deserialize<T>(e.Parameter?.ToString());
        }
    }
}
