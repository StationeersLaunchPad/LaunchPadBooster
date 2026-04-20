using System;
using System.Collections.Generic;
using UnityEngine;

namespace LaunchPadBooster.Async
{
  public class ContinuationQueue
  {
    private readonly object queueLock = new();
    private Queue<Action> waitQueue = new();
    private Queue<Action> execQueue = new();

    public void AddContinuation(Action continuation)
    {
      lock (queueLock)
      {
        waitQueue.Enqueue(continuation);
      }
    }

    public void Execute()
    {
      Queue<Action> queue;
      lock (queueLock)
      {
        (waitQueue, execQueue) = (execQueue, waitQueue);
        queue = execQueue;
      }
      while (queue.Count > 0)
      {
        var continuation = queue.Dequeue();
        try
        {
          continuation();
        }
        catch (Exception ex)
        {
          Debug.LogException(ex);
        }
      }
    }
  }
}