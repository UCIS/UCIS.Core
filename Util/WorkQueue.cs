using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using SysThreadPool = System.Threading.ThreadPool;

namespace UCIS.Util {
	public class WorkQueue : WorkQueue<MethodInvoker> {
		public WorkQueue() : base(Handler) { }
		private static void Handler(MethodInvoker item) { item(); }
	}
	public class WorkQueue<TWork> : IDisposable {
		Queue<TWork> queue = new Queue<TWork>();
		Action<TWork> callback = null;
		int maxIdleWorkers = 0;
		int maxWorkers = 1;
		int idleWorkers = 0;
		int workers = 0;

		public Boolean UseFrameworkThreadpool { get; set; }

		public WorkQueue(Action<TWork> callback) {
			this.callback = callback;
			UseFrameworkThreadpool = true;
		}
		public void Dispose() {
			maxWorkers = 0;
			lock (queue) Monitor.PulseAll(queue);
		}

		public int MaxIdleWorkers {
			get { return maxIdleWorkers; }
			set {
				maxIdleWorkers = value;
				lock (queue) Monitor.PulseAll(queue);
			}
		}
		public int MaxWorkers {
			get { return maxWorkers; }
			set {
				maxWorkers = value;
				lock (queue) Monitor.PulseAll(queue);
			}
		}
		public int TotalWorkers { get { return workers; } }
		public int IdleWorkers { get { return idleWorkers; } }

		public void Enqueue(TWork item) {
			lock (queue) {
				queue.Enqueue(item);
				Monitor.Pulse(queue);
				if (workers < maxWorkers && idleWorkers == 0) StartWorker();
			}
		}
		public void Clear() { lock (queue) queue.Clear(); }
		public int Count { get { lock (queue) return queue.Count; } }

		private void StartWorker() {
			lock (queue) {
				if (workers >= maxWorkers) return;
				if (UseFrameworkThreadpool) {
					SysThreadPool.QueueUserWorkItem(Worker);
				} else {
					(new Thread(Worker)).Start();
				}
				workers++;
			}
		}
		private void RaiseEvent(Action<WorkQueue<TWork>> callback) {
			if (callback != null) callback(this);
		}
		private void Worker(Object state) {
			while (true) {
				TWork item;
				lock (queue) {
					if (workers > maxWorkers) {
						workers--;
						break;
					}
					if (queue.Count == 0) {
						if (idleWorkers > maxIdleWorkers) {
							workers--;
							queue.TrimExcess();
							break;
						}
						idleWorkers++;
						Monitor.Wait(queue);
						idleWorkers--;
						if (queue.Count == 0) continue;
					}
					item = queue.Dequeue();
				}
				callback(item);
			}
		}
	}
}
