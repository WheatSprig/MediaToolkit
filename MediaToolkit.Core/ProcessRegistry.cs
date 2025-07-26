using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MediaToolkit.Core
{

    /// <summary>
    /// 提供进程注册与集中管理功能，可统一追踪并强制终止已注册进程
    /// </summary>
    public static class ProcessRegistry
    {
        // 内部列表：保存所有被追踪的进程对象，使用 List<Process> 存储
        private static readonly List<Process> _tracked = new List<Process>();

        /// <summary>
        /// 将指定进程注册到追踪列表，并在进程退出时自动移除
        /// </summary>
        /// <param name="process">要注册的 Process 实例。若为 null 则直接返回</param>
        public static void Register(Process process)
        {
            // 空值保护
            if (process == null) return;

            // 使用 lock 保证线程安全，防止并发修改 _tracked
            lock (_tracked)
            {
                // 将进程加入追踪列表
                _tracked.Add(process);
            }

            // 订阅进程的 Exited 事件，进程退出时自动从列表移除
            process.Exited += (s, e) =>
            {
                lock (_tracked)
                {
                    _tracked.Remove(process);
                }
            };
        }

        /// <summary>
        /// 强制终止所有已注册且尚未退出的进程，并清空追踪列表
        /// </summary>
        public static void KillAll()
        {
            // 保证线程安全
            lock (_tracked)
            {
                // 使用 ToList() 创建副本，避免在遍历过程中修改集合
                foreach (var p in _tracked.ToList())
                {
                    try
                    {
                        // 仅当进程仍在运行时才调用 Kill
                        if (!p.HasExited)
                            p.Kill();
                    }
                    catch
                    {
                        // 忽略无法终止的进程
                        // 若进程已退出或无权限终止，则忽略异常，防止程序崩溃
                    }
                }
                // 清空追踪列表
                _tracked.Clear();
            }
        }
    }
}
