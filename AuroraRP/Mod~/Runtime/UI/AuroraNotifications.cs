using System.Collections.Generic;
using BoneLib.Notifications;
using UnityEngine;

namespace AuroraRP
{
    /// <summary>
    /// Всплывающие уведомления: игровые попапы BoneLib + список тостов,
    /// который рисует HandMenu (для русских текстов с кириллицей).
    /// </summary>
    public static class AuroraNotifications
    {
        public sealed class Toast
        {
            public string Text;
            public Color Color;
            public float ExpireAt;
            public float CreatedAt;
        }

        private static readonly List<Toast> Active = new List<Toast>();
        private static readonly Queue<(string, Color)> Pending = new Queue<(string, Color)>();

        private static float _lastPopupTime = -10f;
        private const float PopupCooldown = 1.2f;

        public static IReadOnlyList<Toast> Toasts => Active;

        /// <summary>Показать уведомление локальному игроку.</summary>
        public static void Send(string text, Color color, float duration = 4f, bool popup = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Active.Add(new Toast
            {
                Text = text,
                Color = color,
                CreatedAt = Time.realtimeSinceStartup,
                ExpireAt = Time.realtimeSinceStartup + duration
            });

            // Не даём списку разрастаться.
            while (Active.Count > 6)
            {
                Active.RemoveAt(0);
            }

            AuroraLog.Info("[Уведомление] {0}", text);

            if (popup && Time.realtimeSinceStartup - _lastPopupTime > PopupCooldown)
            {
                _lastPopupTime = Time.realtimeSinceStartup;
                ShowBoneLibPopup(text);
            }
        }

        public static void Send(string text) => Send(text, new Color(0.55f, 0.85f, 1f));

        private static void ShowBoneLibPopup(string text)
        {
            try
            {
                Notifier.Send(new Notification
                {
                    Title = new NotificationText("AuroraRP", AuroraUtils.Hex("#FF6B72")),
                    Message = new NotificationText(text, Color.white),
                    Type = NotificationType.Information,
                    PopupLength = 3.5f
                });
            }
            catch (System.Exception e)
            {
                AuroraLog.Exception(e, "BoneLib notification");
            }
        }

        /// <summary>Чистим устаревшие тосты — вызывается из HandMenu.Tick.</summary>
        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (now >= Active[i].ExpireAt)
                {
                    Active.RemoveAt(i);
                }
            }
        }

        public static void Clear()
        {
            Active.Clear();
            Pending.Clear();
        }
    }
}
