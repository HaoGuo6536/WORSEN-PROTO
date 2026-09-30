// ============================================================================
// ShopConfig.cs
// ============================================================================
// PURPOSE:
//   Authors the provisional shop economy separately from the expedition cadence.
//   Plain serialized rules also give unbound test configurations the same defaults
//   without allocating a Unity object inside pure logic.
// ARCHITECTURAL ROLE:
//   Config (§4) · Session · Progression.Shop delegated subtree.
// KEY RESPONSIBILITIES:
//   - Tune pedestals, rerolls, round pricing, inventory and economy upgrades.
// DEPENDENCIES:
//   - Unity serialization only; no runtime state or other systems.
// USAGE NOTES:
//   Runtime reads only. Mirrored asset: Session/Progression/Shop/ShopConfig.
//   Catalogue entries retain ownership of base prices, availability and stack caps.
// ============================================================================
using System;
using UnityEngine;

namespace Worsen.Session.Progression.Shop
{
    [Serializable]
    public sealed class ShopRules
    {
        [SerializeField, Min(1)] private int _pedestals = 4;
        [SerializeField, Min(0)] private int _freeRerolls = 1;
        [SerializeField, Min(0)] private int _rerollPrice = 2;
        [SerializeField, Min(0)] private int _rerollIncrease = 1;
        [SerializeField, Min(0)] private float _roundPriceGrowth = 0.15f;
        [SerializeField, Min(0)] private int _expensivePrice = 8;
        [SerializeField, Min(1)] private int _expensiveUnlockRound = 5;
        [SerializeField, Min(1)] private int _inventorySlots = 3;
        [SerializeField, Min(0)] private int _biggerPocketsSlots = 1;
        [SerializeField, Min(0)] private int _luckyRerolls = 1;
        [SerializeField, Min(0)] private int _shopRerolls = 1;
        [SerializeField, Range(0, 1)] private float _bargainDiscount = 0.25f;
        [SerializeField, Range(0, 1)] private float _loyaltyDiscount = 0.1f;
        [SerializeField, Range(0, 1)] private float _bailPenaltyMultiplier = 0.5f;
        [SerializeField, Min(0)] private int _goldenTouchBonus = 1;
        [SerializeField, Range(0, 1)] private float _interestFraction = 0.1f;
        [SerializeField, Min(0)] private int _interestCap = 5;
        [SerializeField, Range(0, 1)] private float _refundFraction = 0.5f;
        [SerializeField, Min(0)] private int _extraPedestals = 1;
        [SerializeField, Range(0, 1)] private float _businessYieldPerStack = 0.25f;
        public int Pedestals => _pedestals;
        public int FreeRerolls => _freeRerolls;
        public int RerollPrice => _rerollPrice;
        public int RerollIncrease => _rerollIncrease;
        public float RoundPriceGrowth => _roundPriceGrowth;
        public int ExpensivePrice => _expensivePrice;
        public int ExpensiveUnlockRound => _expensiveUnlockRound;
        public int InventorySlots => _inventorySlots;
        public int BiggerPocketsSlots => _biggerPocketsSlots;
        public int LuckyRerolls => _luckyRerolls;
        public int ShopRerolls => _shopRerolls;
        public float BargainDiscount => _bargainDiscount;
        public float LoyaltyDiscount => _loyaltyDiscount;
        public float BailPenaltyMultiplier => _bailPenaltyMultiplier;
        public int GoldenTouchBonus => _goldenTouchBonus;
        public float InterestFraction => _interestFraction;
        public int InterestCap => _interestCap;
        public float RefundFraction => _refundFraction;
        public int ExtraPedestals => _extraPedestals;
        public float BusinessYieldPerStack => _businessYieldPerStack;
    }

    [CreateAssetMenu(menuName = "Worsen/Progression/Shop Config")]
    public sealed class ShopConfig : ScriptableObject
    {
        [SerializeField] private ShopRules _rules = new ShopRules();
        public ShopRules Rules => _rules;
    }
}
