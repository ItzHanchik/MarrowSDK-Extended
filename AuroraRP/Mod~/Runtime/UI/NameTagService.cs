using System;
using System.Collections.Generic;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// Метки над игроками: имя, роль и (по желанию) деньги. Роль видно всем — как в ТЗ.
    /// </summary>
    public class NameTagService
    {
        private sealed class Tag
        {
            public byte PeerId;
            public GameObject Root;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Role;
            public Image Accent;
            public AuroraRoleId LastRole = AuroraRoleId.Citizen;
        }

        private readonly Dictionary<byte, Tag> _tags = new Dictionary<byte, Tag>();
        private float _nextUpdate;

        public void Tick(float dt)
        {
            if (!AuroraConfig.Current.showRoleTags || AuroraRuntime.Net == null || !AuroraRuntime.Net.IsConnected)
            {
                RemoveAll();
                return;
            }

            if (Time.realtimeSinceStartup < _nextUpdate)
            {
                return;
            }

            _nextUpdate = Time.realtimeSinceStartup + 0.25f;

            var peers = AuroraRuntime.Net.Peers;
            var present = new HashSet<byte>();

            var localHead = BoneLib.Player.Head;
            float maxDistance = AuroraConfig.Current.roleTagDistance;

            for (int i = 0; i < peers.Count; i++)
            {
                var peer = peers[i];
                if (peer.Id == AuroraRuntime.LocalId)
                {
                    continue;
                }

                if (!AuroraRuntime.Net.TryGetPeerHead(peer.Id, out Vector3 headPos))
                {
                    continue;
                }

                // Игрок в сессии — метку сохраняем, но прячем, если он далеко.
                present.Add(peer.Id);

                if (localHead != null && (headPos - localHead.position).sqrMagnitude > maxDistance * maxDistance)
                {
                    Hide(peer.Id);
                    continue;
                }

                var tag = EnsureTag(peer.Id);
                if (tag == null)
                {
                    continue;
                }

                tag.Root.SetActive(true);
                tag.Root.transform.position = headPos + Vector3.up * 0.30f;

                if (localHead != null)
                {
                    Vector3 dir = tag.Root.transform.position - localHead.position;
                    if (dir.sqrMagnitude > 0.001f)
                    {
                        tag.Root.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    }
                }

                var rec = AuroraRuntime.State.Get(peer.Id);
                var role = rec != null ? RoleCatalog.Get(rec.Role) : RoleCatalog.Get(AuroraRoleId.Citizen);

                if (tag.Name != null)
                {
                    tag.Name.text = peer.Name;
                }

                if (tag.Role != null)
                {
                    tag.Role.text = role.Name + "  " + AuroraUtils.ShortMoney(rec?.balance ?? 0);
                    tag.Role.color = role.Color;
                }

                if (tag.Accent != null)
                {
                    tag.Accent.color = role.Color;
                }

                tag.LastRole = role.Id;
            }

            // Убираем метки игроков, которых больше нет рядом/в сессии.
            List<byte> dead = null;
            foreach (var pair in _tags)
            {
                if (!present.Contains(pair.Key))
                {
                    (dead ??= new List<byte>()).Add(pair.Key);
                }
            }

            if (dead != null)
            {
                foreach (var id in dead)
                {
                    Destroy(id);
                }
            }
        }

        private Tag EnsureTag(byte peerId)
        {
            if (_tags.TryGetValue(peerId, out var existing) && existing.Root != null)
            {
                return existing;
            }

            try
            {
                var root = new GameObject("AuroraRP_Tag_" + peerId);
                var canvas = UiKit.NewCanvas("Canvas", root.transform, new Vector2(520f, 140f), 0.0012f, 4300);
                var rect = canvas.GetComponent<RectTransform>();

                var panel = UiKit.Panel("Bg", rect, new Vector2(500f, 120f), UiTheme.Panel, 0.88f, true);

                var accent = UiKit.NewImage("Accent", panel.rectTransform, UiTheme.RoundedSoft, UiTheme.Accent, Image.Type.Sliced);
                accent.rectTransform.sizeDelta = new Vector2(10f, 88f);
                accent.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                accent.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                accent.rectTransform.anchoredPosition = new Vector2(18f, 0f);

                var name = UiKit.NewText("Name", panel.rectTransform, "", 36f, UiTheme.Text, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.sizeDelta = new Vector2(430f, 44f);
                name.rectTransform.anchorMin = new Vector2(0f, 1f);
                name.rectTransform.anchorMax = new Vector2(0f, 1f);
                name.rectTransform.pivot = new Vector2(0f, 1f);
                name.rectTransform.anchoredPosition = new Vector2(46f, -14f);

                var role = UiKit.NewText("Role", panel.rectTransform, "", 28f, UiTheme.TextDim, TextAlignmentOptions.MidlineLeft);
                role.rectTransform.sizeDelta = new Vector2(430f, 38f);
                role.rectTransform.anchorMin = new Vector2(0f, 1f);
                role.rectTransform.anchorMax = new Vector2(0f, 1f);
                role.rectTransform.pivot = new Vector2(0f, 1f);
                role.rectTransform.anchoredPosition = new Vector2(46f, -62f);

                var tag = new Tag
                {
                    PeerId = peerId,
                    Root = root,
                    Name = name,
                    Role = role,
                    Accent = accent
                };

                _tags[peerId] = tag;
                return tag;
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "nametag create");
                return null;
            }
        }

        private void Hide(byte peerId)
        {
            if (_tags.TryGetValue(peerId, out var tag) && tag.Root != null)
            {
                tag.Root.SetActive(false);
            }
        }

        private void Destroy(byte peerId)
        {
            if (_tags.TryGetValue(peerId, out var tag))
            {
                if (tag.Root != null)
                {
                    UnityEngine.Object.Destroy(tag.Root);
                }

                _tags.Remove(peerId);
            }
        }

        private void RemoveAll()
        {
            foreach (var id in new List<byte>(_tags.Keys))
            {
                Destroy(id);
            }
        }
    }
}
