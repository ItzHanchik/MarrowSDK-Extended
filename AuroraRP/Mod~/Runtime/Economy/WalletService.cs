using System;

namespace AuroraRP
{
    /// <summary>
    /// Счёт каждого игрока. Хост — источник правды; клиенты получают изменения через AuroraAuthority.
    /// </summary>
    public class WalletService
    {
        private readonly AuroraState _state;

        /// <summary>(игрок, было, стало, причина)</summary>
        public event Action<byte, long, long, string> OnBalanceChanged;

        public WalletService(AuroraState state)
        {
            _state = state;
        }

        public long GetBalance(byte playerId)
        {
            return _state.Get(playerId)?.balance ?? AuroraConfig.Current.startBalance;
        }

        public PlayerRecord Ensure(byte playerId, string name = null)
        {
            var rec = _state.GetOrCreate(playerId, name ?? ("Player " + playerId));
            return rec;
        }

        public void SetBalance(byte playerId, long value, string reason)
        {
            var rec = Ensure(playerId);
            long old = rec.balance;
            rec.balance = Math.Max(0, value);
            _state.MarkPlayerChanged(rec);
            Raise(playerId, old, rec.balance, reason);
        }

        public void Add(byte playerId, long amount, string reason)
        {
            if (amount == 0)
            {
                return;
            }

            var rec = Ensure(playerId);
            long old = rec.balance;
            rec.balance = Math.Max(0, rec.balance + amount);
            _state.MarkPlayerChanged(rec);
            Raise(playerId, old, rec.balance, reason);
        }

        public bool CanAfford(byte playerId, long amount) => GetBalance(playerId) >= amount;

        public bool TrySpend(byte playerId, long amount, string reason)
        {
            if (amount <= 0)
            {
                return true;
            }

            var rec = Ensure(playerId);
            if (rec.balance < amount)
            {
                return false;
            }

            long old = rec.balance;
            rec.balance -= amount;
            _state.MarkPlayerChanged(rec);
            Raise(playerId, old, rec.balance, reason);
            return true;
        }

        /// <summary>Атомарный перевод между игроками.</summary>
        public bool TryTransfer(byte from, byte to, long amount, string reason, out string error)
        {
            error = null;

            if (from == to)
            {
                error = AuroraL.Get("money.transfer.fail.self");
                return false;
            }

            if (amount <= 0)
            {
                error = AuroraL.Get("money.transfer.fail.sum");
                return false;
            }

            var max = AuroraConfig.Current.transferMax;
            if (max > 0 && amount > max)
            {
                amount = max;
            }

            var fromRec = Ensure(from);
            var toRec = Ensure(to);

            if (fromRec.balance < amount)
            {
                error = AuroraL.Get("money.transfer.fail.funds");
                return false;
            }

            long fromOld = fromRec.balance;
            long toOld = toRec.balance;

            fromRec.balance -= amount;
            toRec.balance += amount;

            _state.MarkPlayerChanged(fromRec);
            _state.MarkPlayerChanged(toRec);

            Raise(from, fromOld, fromRec.balance, reason);
            Raise(to, toOld, toRec.balance, reason);

            AuroraLog.Info("Перевод {0}: {1} -> {2} ({3})", AuroraUtils.Money(amount), fromRec.name, toRec.name, reason);
            return true;
        }

        private void Raise(byte playerId, long old, long now, string reason)
        {
            if (old == now)
            {
                return;
            }

            try
            {
                OnBalanceChanged?.Invoke(playerId, old, now, reason);
            }
            catch (Exception e)
            {
                AuroraLog.Exception(e, "OnBalanceChanged");
            }

            AuroraStorage.LogTransaction(playerId, old, now, reason);
        }
    }
}
