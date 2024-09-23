using BiliApi;
using BiliveDanmakuAgent;
using LiveEventMarker.Model;
using QRCoder;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace LiveEventMarker
{
    internal class Program
    {
        static string cookies = "";
        static BiliSession bsession;
        static int LiveroomId = 35298;   //直播间
        static long UserId = 89380160; //牙刷搬运工
        static Dictionary<DateTime, List<LiveSegment>> lives = new Dictionary<DateTime, List<LiveSegment>>();
        static DateTime CurrentLive = DateTime.MinValue;
        static LiveSegment CurrentSegment;
        static List<long> oprIdL = new List<long>();

        static void Main(string[] args)
        {
            Console.WriteLine("初始化...");
            BLogin();
            DanmakuApi dc = new DanmakuApi(LiveroomId,  //直播间号
                cookies                                   //登录Cookie
                );
            dc.ConnectAsync().Wait();                //连接到弹幕服务器

            dc.Superchat += Dc_Superchat;
            dc.GuardBuy += Dc_GuardBuy;
            dc.LiveStartEvent += Dc_LiveStartEvent;
            dc.LiveEndEvent += Dc_LiveEndEvent;
            Console.WriteLine("弹幕连接成功");
            BiliSpaceDynamic biliDyn = new BiliSpaceDynamic(UserId, bsession);
            while (true)
            {
                try
                {
                    if(lives.Count == 0)
                    {
                        //Console.WriteLine($"Short Ended: no data");
                        Thread.Sleep(30 * 1000);            //每半分钟抓取一次新动态
                        continue;
                    }
                    if (lives.First().Value.Last().DataPending)
                    {
                        //Console.WriteLine($"Short Ended: data pending");
                        Thread.Sleep(30 * 1000);            //每半分钟抓取一次新动态
                        continue;
                    }
                    Console.WriteLine($"{DateTime.Now.ToString("yyyyMMdd-HH:mm:ss")} 抓取当前动态...");
                    var latestDyn = biliDyn.getLatest();
                    foreach (var dyn in latestDyn)
                    {
                        List<DateTime> delList = new List<DateTime>();
                        if (!dyn.dynamic.Contains("录播")) continue; //不是录播，排除
                        foreach (var liv in lives)
                        {
                            bool isAllFinished = true;
                            foreach (var seg in liv.Value)
                            {
                                if (seg.DataPending)
                                {
                                    isAllFinished = false;
                                    break;
                                }
                            }
                            if (!isAllFinished)
                                continue;

                            string datestr = liv.Key.ToString("yyyyMMdd");
                            if (!dyn.vinfo.discription.Contains(datestr))
                                continue; //录播日期不对，排除

                            Console.WriteLine("找到" + datestr + "对应视频！编辑评论...");
                            Console.WriteLine("----------------------------------------");
                            {
                                // 就是你了，发评论！
                                StringBuilder sb = new StringBuilder();
                                sb.AppendLine("<萌刷高光时刻>");
                                sb.AppendLine("[SC]");
                                foreach (var seg in liv.Value)
                                {
                                    foreach (var sc in seg.Superchat)
                                    {
                                        var dur = sc.Time - seg.startTime;
                                        string durstr = dur.TotalHours >= 1 ? ($"{dur.Hours}:{dur.Minutes}:{dur.Seconds}") : ($"{dur.Minutes}:{dur.Seconds}");
                                        sb.AppendLine($"{durstr} {sc.Name}");
                                    }
                                }
                                sb.AppendLine("[舰队]");
                                foreach (var seg in liv.Value)
                                {
                                    foreach (var gb in seg.Guardbuy)
                                    {
                                        var dur = gb.Time - seg.startTime;
                                        string durstr = dur.TotalHours >= 1 ? ($"{dur.Hours}:{dur.Minutes}:{dur.Seconds}") : ($"{dur.Minutes}:{dur.Seconds}");
                                        sb.AppendLine($"{durstr} {gb.Name}");
                                    }
                                }
                                sb.AppendLine("\n上述信息自动生成 by @亿只鸡蛋_Egg");
                                Console.WriteLine(sb.ToString());
                                Console.WriteLine("----------------------------------------");
                                Console.WriteLine("提交评论...");
                                if (oprIdL.Contains(dyn.vinfo.av))
                                {
                                    Console.WriteLine("重复操作！！！！！检查代码逻辑！！！！");
                                    continue;
                                }
                                oprIdL.Add(dyn.vinfo.av);
                                // 发射！
                                var result = bsession.sendComment(dyn.vinfo.av, sb.ToString());
                                Console.WriteLine(result.Substring(128));
                            }
                            delList.Add(liv.Key);
                        }

                        // 移除操作过的记录
                        foreach (var del in delList)
                        {
                            lives.Remove(del);
                        }
                    }
                    Console.WriteLine($"{DateTime.Now.ToString("yyyyMMdd-HH:mm:ss")} 本轮操作结束。");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                    Console.WriteLine($"Stack: {e.StackTrace}");
                }
                Thread.Sleep(30 * 1000);            //每半分钟抓取一次新动态
            }
        }

        private static void Dc_LiveEndEvent(object sender, BiliveDanmakuAgent.Model.RoomEventArgs e)
        {
            CurrentSegment.DataPending = false;
            Console.WriteLine($"- 直播{CurrentLive.ToString("yyyy-MM-dd-HH:mm:ss")}结束");
            CurrentLive = DateTime.MinValue;
        }

        private static void Dc_LiveStartEvent(object sender, BiliveDanmakuAgent.Model.RoomEventArgs e)
        {
            if (CurrentLive == DateTime.MinValue)
            {
                //新的直播
                CurrentLive = DateTime.Now;
                var seglist = new List<LiveSegment>();
                lives.Add(CurrentLive, seglist);
                CurrentSegment = new LiveSegment
                {
                    startTime = CurrentLive,
                    Guardbuy = new List<Event>(),
                    Superchat = new List<Event>(),
                };
                seglist.Add(CurrentSegment);
                Console.WriteLine($"+ 新直播{CurrentLive.ToString("yyyy-MM-dd-HH:mm:ss")}");
            }
            else
            {
                //老直播新片段，可能发生了重连。
                var live = lives.Last().Value;
                var segStart = DateTime.Now;
                CurrentSegment.DataPending = false;
                CurrentSegment = new LiveSegment
                {
                    startTime = segStart,
                    Guardbuy = new List<Event>(),
                    Superchat = new List<Event>(),
                };
                live.Add(CurrentSegment);
                Console.WriteLine($" + 新片段{segStart.ToString("yyyy-MM-dd-HH:mm:ss")}");
            }
        }

        private static void Dc_GuardBuy(object sender, BiliveDanmakuAgent.Model.DanmakuReceivedEventArgs e)
        {
            Console.WriteLine($"  + 新舰队 {e.Danmaku.UserName}#{e.Danmaku.UserID}");
            CurrentSegment.Guardbuy.Add(
                new Event
                {
                    Name = e.Danmaku.UserName,
                    Time = DateTime.Now,
                    Uid = e.Danmaku.UserID,
                });
        }

        private static void Dc_Superchat(object sender, BiliveDanmakuAgent.Model.DanmakuReceivedEventArgs e)
        {
            Console.WriteLine($"  + 新SC {e.Danmaku.UserName}#{e.Danmaku.UserID}");
            CurrentSegment.Superchat.Add(
                new Event
                {
                    Name = e.Danmaku.UserName,
                    Time = DateTime.Now,
                    Uid = e.Danmaku.UserID,
                });
        }

        static void BLogin()
        {
            if (File.Exists("bili_login.key"))
            {
                cookies = File.ReadAllText("bili_login.key");
            }
            var qr = new BiliApi.Auth.QRLogin(cookies);
            if (!qr.LoggedIn)
            {
                qr = new BiliApi.Auth.QRLogin();
                {
                    QRCodeGenerator qrGenerator = new QRCodeGenerator();
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(new PayloadGenerator.Url(qr.QRToken.ScanUrl), QRCodeGenerator.ECCLevel.M);
                    AsciiQRCode qrCode = new AsciiQRCode(qrCodeData);
                    Console.WriteLine(qrCode.GetGraphic(1));
                }
                Console.WriteLine("扫描上面的二维码，登录您的B站账号。");
                qr.Login();
                Console.WriteLine("登录成功");
            }
            else
            {
                Console.WriteLine("登录成功");
            }

            bsession = new BiliSession(qr.Cookies);

            File.WriteAllText("bili_login.key", bsession.GetCookieString());
        }
    }
}
