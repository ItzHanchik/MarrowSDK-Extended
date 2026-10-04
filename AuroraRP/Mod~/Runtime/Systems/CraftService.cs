using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow.Pool;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AuroraRP
{
    /// <summary>
    /// «Маники» — принтеры денег. Печатают деньги владельцу, пока он рядом.
    /// Гангстер может украсть принтер (взять в руку и нажать B).
    /// </summary>
    public class CraftService
    {
        private readonly AuroraState _state;
        private readonly Dictionary<int, PrinterBehaviour> _printers = new Dictionary<int, PrinterBehaviour>();
        private byte _heldPrinterOwner = 255;
        private AuroraComponent _heldPrinter;
        private bool _subscribed;
        private float _nextScan;

        public CraftService(AuroraState state)
        {
            _state = state;
        }

        public void OnLevelUnloaded()
        {
            _printers.Clear();
            _heldPrinter = null;
        }

        public void Tick(float dt)
        {
            if (!_subscribed && AuroraRuntime.Input != null)
            {
                AuroraRuntime.Input.OnBPressed += OnButtonB;
                _subscribed = true;
            }

            if (Time.realtimeSinceStartup < _nextScan)
            {
                return;
            }

            _nextScan = Time.realtimeSinceStartup + 4f;
            Scan();
        }

        private void Scan()
        {
            string barcode = AuroraConfig.Current.printerBarcode;
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return;
            }

            try
            {
                var poolees = UnityEngine.Object.FindObjectsOfType<Poolee>();
                if (poolees == null)
                {
                    return;
                }

                for (int i = 0; i < poolees.Length; i++)
                {
                    var poolee = poolees[i];
                    if (poolee == null)
                    {
                        continue;
                    }

                    string id = null;
                    try
                    {
                        var crate = poolee.SpawnableCrate;
                        id = crate?.Barcode.ID;
                    }
                    catch (Exception)
                    {
                    }

                    if (string.IsNullOrEmpty(id) || !id.Equals(barcode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var go = poolee.gameObject;
                    int instanceId = go.GetInstanceID();

                    if (_printers.ContainsKey(instanceId))
                    {
                        continue;
                    }

                    string hash = DoorService.BuildDoorHash(go, id);
                    var behaviour = new PrinterBehaviour(go, hash, this);
                    AuroraComponent.Attach(go, "printer", behaviour);
                    _printers[instanceId] = behaviour;

                    AuroraLog.Info("Найден принтер денег (hash {0})", hash);
                }
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "printer scan");
            }
        }

        public void OnGrabbed(GameObject go, Il2CppSLZ.Marrow.Hand hand)
        {
            var component = go.GetComponentInParent<AuroraComponent>();
            if (component == null || component.Kind != "printer")
            {
                return;
            }

            _heldPrinter = component;
            var behaviour = component.GetHandler<PrinterBehaviour>();
            if (behaviour != null)
            {
                AuroraNotifications.Send(behaviour.StatusLine, UiTheme.Accent);
            }
        }

        public void OnReleased(Il2CppSLZ.Marrow.Hand hand)
        {
            _heldPrinter = null;
        }

        private void OnButtonB(Il2CppSLZ.Marrow.Interaction.Handedness hand)
        {
            if (_heldPrinter == null)
            {
                return;
            }

            var behaviour = _heldPrinter.GetHandler<PrinterBehaviour>();
            behaviour?.OnButtonB();
        }

        /// <summary>Печать денег (вызывается принтером).</summary>
        public void Payout(string hash, byte ownerId, int amount)
        {
            AuroraRuntime.Net.SendPrinterPayout(hash, ownerId, amount);
        }

        public void ApplyPayout(string hash, byte ownerId, int amount)
        {
            AuroraRuntime.Wallet.Add(ownerId, amount, "принтер денег");
            AuroraRuntime.Notify(ownerId, "+" + AuroraUtils.Money(amount), UiTheme.Money);
        }

        /// <summary>Кража принтера: меняем владельца (проверка роли — на хосте).</summary>
        public void ApplySteal(string hash, byte newOwner)
        {
            if (!AuroraRuntime.Roles.Has(newOwner, RoleAbilities.StealPrinters))
            {
                AuroraRuntime.Notify(newOwner, AuroraL.Get("common.locked"), UiTheme.Warning);
                return;
            }

            foreach (var printer in _printers.Values)
            {
                if (printer.Hash == hash)
                {
                    printer.SetOwner(newOwner);
                    break;
                }
            }

            AuroraRuntime.NotifyAll(AuroraL.Get("role.Gangster") + ": " + AuroraL.Get("common.take"), UiTheme.Danger);
        }
    }

    /// <summary>Принтер денег на объекте из палета.</summary>
    public class PrinterBehaviour : IAuroraTickable, IAuroraDisposable
    {
        public string Hash { get; }
        public byte OwnerId { get; private set; } = 255;

        private readonly GameObject _root;
        private readonly CraftService _service;
        private readonly List<GameObject> _signs = new List<GameObject>();
        private readonly List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();

        private float _accumulator;
        private long _printed;

        public PrinterBehaviour(GameObject root, string hash, CraftService service)
        {
            _root = root;
            Hash = hash;
            _service = service;
            BuildSign();
        }

        public string StatusLine
        {
            get
            {
                if (OwnerId == 255)
                {
                    return AuroraL.Get("common.nobody") + " · " + AuroraUtils.Money(_printed);
                }

                var owner = AuroraRuntime.State.Get(OwnerId);
                return AuroraL.Get("common.owner") + ": " + (owner?.name ?? "?") + " · " + AuroraUtils.Money(_printed);
            }
        }

        private void BuildSign()
        {
            try
            {
                var go = new GameObject("AuroraRP_PrinterSign");
                go.transform.SetParent(_root.transform, true);
                go.transform.position = _root.transform.position + Vector3.up * 0.35f;
                go.transform.rotation = _root.transform.rotation;

                var canvas = UiKit.NewCanvas("Canvas", go.transform, new Vector2(410f, 120f), 0.001f, 4200);
                var rect = canvas.GetComponent<RectTransform>();

                UiKit.Panel("Bg", rect, new Vector2(400f, 110f), UiTheme.Panel, 0.92f, false);

                var text = UiKit.NewText("Text", rect, "", 30f, UiTheme.Money, TextAlignmentOptions.Midline);
                text.rectTransform.sizeDelta = new Vector2(380f, 90f);

                _signs.Add(go);
                _texts.Add(text);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "printer sign");
            }
        }

        public void SetOwner(byte ownerId)
        {
            OwnerId = ownerId;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _texts.Count; i++)
            {
                _texts[i].text = "💰 " + StatusLine;
            }
        }

        public void Tick(AuroraComponent component, float dt)
        {
            if (OwnerId == 255)
            {
                return;
            }

            var owner = AuroraRuntime.State.Get(OwnerId);
            if (owner == null)
            {
                return;
            }

            // Печатаем только если владелец рядом (8 метров) — иначе смысла стоять у принтера нет.
            var head = BoneLib.Player.Head;
            if (OwnerId == AuroraRuntime.LocalId && head != null)
            {
                if ((_root.transform.position - head.position).sqrMagnitude > 64f)
                {
                    return;
                }
            }

            _accumulator += dt;
            float interval = Mathf.Max(1f, AuroraConfig.Current.printerInterval);

            while (_accumulator >= interval)
            {
                _accumulator -= interval;
                int payout = AuroraConfig.Current.printerPayout;
                _printed += payout;
                _service.Payout(Hash, OwnerId, payout);
                Refresh();
            }
        }

        public void OnButtonB()
        {
            // Воровать может только гангстер.
            if (!AuroraRuntime.Roles.Has(AuroraRuntime.LocalId, RoleAbilities.StealPrinters))
            {
                AuroraNotifications.Send(AuroraL.Get("common.locked"), UiTheme.Warning);
                return;
            }

            AuroraRuntime.Net.SendPrinterSteal(Hash, AuroraRuntime.LocalId);
        }

        public void Dispose()
        {
            for (int i = 0; i < _signs.Count; i++)
            {
                if (_signs[i] != null)
                {
                    UnityEngine.Object.Destroy(_signs[i]);
                }
            }

            _signs.Clear();
            _texts.Clear();
        }
    }
}
