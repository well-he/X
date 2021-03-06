﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Log;
using NewLife.Security;
using Xunit;

namespace XUnitTest.Data
{
    public class FlowIdTests
    {
        [Fact]
        public void NewId()
        {
            var f = new FlowId();
            var id = f.NewId();

            var time = id >> 22;
            var tt = f.StartTimestamp.AddMilliseconds(time);
            Assert.True(tt <= DateTime.Now);

            var wid = (id >> 12) & 0x3FF;
            Assert.Equal(f.WorkerId, wid);

            var seq = id & 0x0FFF;
            Assert.Equal(f.Sequence, seq);

            // 时间转编号
            var id2 = f.GetId(tt);
            Assert.Equal(id >> 22, id2 >> 22);

            // 分析
            var rs = f.TryParse(id, out var t, out var w, out var s);
            Assert.True(rs);
            Assert.Equal(tt, t);
            Assert.Equal(wid, w);
            Assert.Equal(seq, s);
        }

        [Fact]
        public void ValidRepeat()
        {
            var sw = Stopwatch.StartNew();

            var ws = new ConcurrentBag<Int32>();
            var repeat = new ConcurrentBag<Int64>();
            var hash = new ConcurrentHashSet<Int64>();

            var ts = new List<Task>();
            for (var k = 0; k < 10; k++)
            {
                ts.Add(Task.Run(() =>
                {
                    var f = new FlowId { StartTimestamp = new DateTime(2020, 1, 1), WorkerId = Rand.Next() & 0x3FF };
                    ws.Add(f.WorkerId);

                    for (var i = 0; i < 100_000; i++)
                    {
                        var id = f.NewId();
                        if (!hash.TryAdd(id)) repeat.Add(id);
                    }
                }));
            }
            Task.WaitAll(ts.ToArray());

            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 10_000);
            var count = repeat.Count;
            Assert.Equal(0, count);
        }

        [Fact]
        public void Benchmark()
        {
            var sw = Stopwatch.StartNew();

            var count = 10_000_000L;

            var ts = new List<Task>();
            for (var i = 0; i < Environment.ProcessorCount; i++)
            {
                ts.Add(Task.Run(() =>
                {
                    var f = new FlowId { BlockOnSampleTime = false };

                    for (var i = 0; i < count; i++)
                    {
                        var id = f.NewId();
                    }
                }));
            }

            Task.WaitAll(ts.ToArray());

            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 10_000);

            count *= ts.Count;
            XTrace.WriteLine("生成 {0:n0}，耗时 {1}，速度 {2:n0}tps", count, sw.Elapsed, count * 1000 / sw.ElapsedMilliseconds);
        }
    }
}