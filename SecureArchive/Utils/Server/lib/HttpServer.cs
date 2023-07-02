// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Server.lib.model;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;

namespace SecureArchive.Utils.Server.lib;

public class HttpServer
{
    #region Fields

    private TcpListener Listener = null!;
    private HttpProcessor Processor;
    //private bool IsActive = true;

    #endregion

    //private static readonly ILog log = LogManager.GetLogger(typeof(HttpServer));    
    private bool Alive = true;
    public ILogger Logger;

    private BehaviorSubject<bool> _running = new BehaviorSubject<bool>(false);
    public IObservable<bool> Running => _running;

    //private WeakReference<IReportOutput> mReportOutput;
    //private IReportOutput ReportOutput => mReportOutput?.GetValue();

    #region Public Methods
    public HttpServer(List<Route> routes, ILogger logger)
    {
        Processor = new HttpProcessor(); ;
        //mReportOutput = new WeakReference<IReportOutput>(reportOutput);
        Logger = logger;

        foreach (var route in routes)
        {
            Processor.AddRoute(route);
        }
    }

    //public void Listen()
    //{
    //    this.Listener = new TcpListener(IPAddress.Any, this.Port);
    //    this.Listener.Start();
    //    while (this.IsActive)
    //    {
    //        TcpClient s = this.Listener.AcceptTcpClient();
    //        Thread thread = new Thread(() =>
    //        {
    //            this.Processor.HandleClient(s);
    //        });
    //        thread.Start();
    //        Thread.Sleep(1);
    //    }
    //}

    public bool Start(int port)
    {
        if(_running.Value) { 
            return false; 
        }
        try
        {
            Alive = true;
            _running.OnNext(true);
            Listener = new TcpListener(IPAddress.Any, port);
            Listener.Start();
            Logger.Info($"HTTP Server Running... Port={port}");
        }
        catch (Exception e)
        {
            Logger.Error(e, "cannot start server.");
            Stop();
            return false;
        }
        Task.Run(async () =>
        {
            while (Alive)
            {
                try
                {
                    TcpClient s = await Listener.AcceptTcpClientAsync();
                    Processor.HandleClient(s);
                    Logger.Debug("Processer Completed.");
                }
                catch (Exception e)
                {
                    if (Alive)
                    {
                        //ReportOutput?.ErrorOutput(e.ToString());
                        Logger.Error(e, "cannot listen.");
                    }
                }
            }
            lock (this)
            {
                _running.OnNext(false);
                Listener?.Stop();
                Logger.Info("HTTP Server Stopped.");
            }
        });
        return true;
    }

    public void Stop()
    {
        lock (this)
        {
            if(!Alive) {
                return;
            }
            Alive = false;
            _running.OnNext(false);
            Listener?.Stop();
        }
    }
    #endregion

}



