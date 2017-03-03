﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SaferMutex.Tests.Utils
{
    public class Background
    {
        private readonly Thread _backgroundThread;
        private Exception _backgroundException = null;

        private Background(Action action)
        {

            _backgroundThread = new Thread(() => ActionWrapper(action));
            _backgroundThread.Start();
        }

        public static Background Start(Action action)
        {
            return new Background(action);
        }

        public void Wait()
        {
            _backgroundThread.Join();
            if (_backgroundException != null)
                throw new AggregateException(_backgroundException);
        }

        public bool Wait(int millisecoundsTimeout)
        {
            var result = _backgroundThread.Join(millisecoundsTimeout);
            if (_backgroundException != null)
                throw new AggregateException(_backgroundException);

            return result;
        }

        private void ActionWrapper(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                _backgroundException = e;
            }
        }
    }
}
