// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Server.lib.model;
using System.Net;
using System.Net.Sockets;

namespace SecureArchive.Utils.Server.lib;

public class HttpServer
{
    #region Fields

    private int Port;
    private TcpListener? Listener;
    private HttpProcessor Processor;
    //private bool IsActive = true;

    #endregion

    //private static readonly ILog log = LogManager.GetLogger(typeof(HttpServer));    
    private bool Alive = true;
    public ILogger Logger;

    //private WeakReference<IReportOutput> mReportOutput;
    //private IReportOutput ReportOutput => mReportOutput?.GetValue();

    #region Public Methods
    public HttpServer(int port, List<Route> routes, ILogger logger)
    {
        Port = port;
        Processor = new HttpProcessor(logger); ;
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

    public bool Start()
    {
        try
        {
            Listener = new TcpListener(IPAddress.Any, Port);
            Listener.Start();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Start");
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
                }
                catch (Exception e)
                {
                    if (Alive)
                    {
                        //ReportOutput?.ErrorOutput(e.ToString());
                        Logger.LogError(e, "Listen");
                    }
                }
            }
            lock (this)
            {
                Listener?.Stop();
                Listener = null;
            }
        });
        return true;
    }

    public void Stop()
    {
        Alive = false;
        lock (this)
        {
            Listener?.Stop();
            Listener = null;
        }
    }
    #endregion

}



