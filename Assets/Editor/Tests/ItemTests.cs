using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ItemTests
{
    private GameObject _go;
    private Inventory _inv;
    private ItemDefinition _potion, _sword;

    [SetUp]
    public void SetUp()
    {
        _potion = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/health_potion.asset");
        _sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Items/knight_sword.asset");
        _go = new GameObject("InventoryTest");
        _inv = _go.AddComponent<Inventory>();
        _inv.capacity = 4;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void LosApilablesSeAgrupanHastaSuMaximo()
    {
        Assume.That(_potion.IsStackable);
        int left = _inv.Add(_potion, _potion.maxStack + 1);
        Assert.AreEqual(0, left);
        Assert.AreEqual(_potion.maxStack + 1, _inv.Count(_potion));
        Assert.AreEqual(2, _inv.UsedSlots);
    }

    [Test]
    public void LoQueNoCabeSeDevuelve()
    {
        Assume.That(!_sword.IsStackable);
        int left = _inv.Add(_sword, 6);
        Assert.AreEqual(2, left);
        Assert.AreEqual(0, _inv.FreeSlots);
    }

    [Test]
    public void QuitarDescuentaLaCantidad()
    {
        _inv.Add(_potion, 3);
        Assert.IsTrue(_inv.Remove(_potion, 2));
        Assert.AreEqual(1, _inv.Count(_potion));
        Assert.IsFalse(_inv.Remove(_potion, 5));
    }

    [Test]
    public void ElArmaSeDesgastaSeRompeYSeRepara()
    {
        Assume.That(_sword.HasDurability);
        var stack = new ItemStack(_sword, 1);
        Assert.AreEqual(_sword.maxDurability, stack.durability, 0.001f);
        Assert.IsFalse(stack.Wear(_sword.maxDurability * 0.5f));
        Assert.IsTrue(stack.Wear(_sword.maxDurability));
        Assert.IsTrue(stack.IsBroken);
        stack.Repair();
        Assert.IsFalse(stack.IsBroken);
        Assert.AreEqual(1f, stack.Durability01, 0.001f);
    }

    [Test]
    public void ClonarNoCompartePiezas()
    {
        var a = new ItemStack(_sword, 1);
        var b = a.Clone();
        b.Wear(10f);
        b.runes.Add(_potion);
        Assert.AreNotEqual(a.durability, b.durability);
        Assert.AreEqual(0, a.runes.Count);
    }
}
