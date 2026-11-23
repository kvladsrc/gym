using System.Collections.Generic;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// The message line under the top bar. A new text used to wipe the shown one, so of three tasks
    /// done at once the players saw only the first, and a gem's news wiped the task's (the
    /// interface plan's 3.4, 2026-11-11). Now news wait in a queue: each stands at least
    /// <see cref="Least"/> seconds before the next takes its place, and the same words twice in a
    /// row show once. An answer to what the players just did (why a button is pale, where a hero
    /// cannot go) shows at once; news it cuts short come back after it.
    /// </summary>
    public sealed partial class Hud
    {
        /// <summary>How long a message stands at least while others wait.</summary>
        const float Least = 2.5f;

        struct Message
        {
            public string Text;
            public float Seconds;
            public bool News;
        }

        readonly List<Message> waiting = new List<Message>();
        float toastUntil, toastShown;
        bool toastNews;

        /// <summary>An answer to what the players just did: it shows at once.</summary>
        public void Toast(string text, float seconds = 3.5f) => Say(new Message { Text = text, Seconds = seconds });

        /// <summary>News (a task done, a gem, a find): they wait their turn behind the shown message.</summary>
        public void News(string text, float seconds = 4f) => Say(new Message { Text = text, Seconds = seconds, News = true });

        /// <summary>The message line and its queue gone at once.</summary>
        public void ClearToast()
        {
            toastUntil = 0f;
            waiting.Clear();
        }

        void Say(Message message)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                ClearToast();
                return;
            }
            float now = ViewClock.Now;
            bool shown = toastUntil > now;
            if (shown && toast.text == message.Text)
            {
                toastUntil = Mathf.Max(toastUntil, now + message.Seconds);
                return;
            }
            if (message.News && (shown || waiting.Count > 0))
            {
                if (waiting.Count == 0 || waiting[waiting.Count - 1].Text != message.Text)
                    waiting.Add(message);
                return;
            }
            // News cut short by an answer come back after it, for what they had left.
            if (shown && toastNews && !message.News)
                waiting.Insert(0, new Message { Text = toast.text, Seconds = Mathf.Max(Least, toastUntil - now), News = true });
            Show(message, now);
        }

        void Show(Message message, float now)
        {
            toast.text = message.Text;
            toastShown = now;
            toastUntil = now + message.Seconds;
            toastNews = message.News;
        }

        void UpdateToast()
        {
            float now = ViewClock.Now;
            if (waiting.Count > 0)
            {
                // With others waiting the shown message stands its least, then fades for the next.
                toastUntil = Mathf.Min(toastUntil, toastShown + Least);
                if (toastUntil <= now)
                {
                    Message next = waiting[0];
                    waiting.RemoveAt(0);
                    Show(next, now);
                }
            }
            float left = toastUntil - now;
            toast.style.opacity = Mathf.Clamp01(Mathf.Min((now - toastShown) / 0.15f, left / 0.4f));
        }
    }
}
