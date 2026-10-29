// Copyright (C) 2016 by David Jeske, Barend Erasmus and donated to the public domain

using Microsoft.Extensions.Logging;
using SecureArchive.Utils.Server.lib.model;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;

namespace SecureArchive.Utils.Server.lib;

public class HttpServer
{
    #region Fields

    private TcpListener? Listener = null;
    private HttpProcessor Processor;
    //private bool IsActive = true;

    #endregion

    //private static readonly ILog log = LogManager.GetLogger(typeof(HttpServer));
    private bool Alive = true;
    public UtLog Logger;
    /// <summary>true なら HTTPS リスナーとして起動 (cert 必須)。</summary>
    public bool IsSecure { get; }

    private BehaviorSubject<bool> _running = new BehaviorSubject<bool>(false);
    public IObservable<bool> Running => _running;

    private int IdGenerator = 0;
    private int GenerateId() {
        return Interlocked.Increment(ref IdGenerator);
    }


    //private WeakReference<IReportOutput> mReportOutput;
    //private IReportOutput ReportOutput => mReportOutput?.GetValue();

    #region Public Methods
    public HttpServer(List<Route> routes, UtLog logger)
        : this(routes, logger, null) {
    }

    public HttpServer(List<Route> routes, UtLog logger, X509Certificate2? certificate) {
        Processor = (certificate != null)
            ? new SslHttpProcessor(certificate)
            : new HttpProcessor();
        IsSecure = certificate != null;
        Logger = logger;

        foreach (var route in routes) {
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

    CancellationTokenSource? _cts = null;

    public bool Start(int port) {
        if (_running.Value) {
            return false;
        }
        try {
            Alive = true;
            _running.OnNext(true);
            Listener = new TcpListener(IPAddress.Any, port);
            Listener.Start();
            Logger.Info($"{(IsSecure ? "HTTPS" : "HTTP")} Server Running... Port={port}");
        }
        catch (Exception e) {
            Logger.Error(e, "cannot start server.");
            Stop();
            _running.OnNext(false);
            return false;
        }
        Task.Run(async () => {
            _cts = new CancellationTokenSource();
            while (Alive) {
                try {
                    TcpClient s = await Listener.AcceptTcpClientAsync(_cts.Token);
                    int id = GenerateId();
                    Processor.HandleClient(id, s);
                }
                catch (Exception e) {
                    if (Alive) {
                        //ReportOutput?.ErrorOutput(e.ToString());
                        Logger.Error(e, "cannot listen.");
                    }
                }
            }
            lock (this) {
                Listener?.Stop();
                Listener = null;
                _cts = null;
                _running.OnNext(false);
                Logger.Info("HTTP Server Stopped.");
            }
        });
        return true;
    }

    private void Stop() {
        lock (this) {
            if (!Alive) {
                return;
            }
            Alive = false;
            _cts?.Cancel();
            Listener?.Stop();
        }
    }

    public async Task StopAsync() {
        Stop();
        try {
            await _running.FirstAsync(it => !it)
                .Timeout(TimeSpan.FromSeconds(3));
        }
        catch (Exception e) {
            Logger.Error(e, "StopAsync failed");
        }
    }
    #endregion

}



