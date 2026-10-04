using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class StatsTests
{
    private GameObject _go;
    private CharacterStats _stats;
    private CharacterClass _knight;

    [SetUp]
    public void SetUp()
    {
        _knight = AssetDatabase.LoadAssetAtPath<CharacterClass>("Assets/Data/Classes/Knight_Caballero.asset");
        _go = new GameObject("StatsTest");
        _stats = _go.AddComponent<CharacterStats>();
        _stats.InitFromClass(_knight);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_go);

    [Test]
    public void VidaYManaMaximosCuentanLaClaseUnaSolaVez()
    {
        Assert.AreEqual(_stats.baseMaxHealth + _knight.baseHealth * _stats.healthPerPoint, _stats.MaxHealth, 0.001f);
        Assert.AreEqual(_stats.baseMaxMana + _knight.baseMana * _stats.manaPerPoint, _stats.MaxMana, 0.001f);
        Assert.AreEqual(_stats.MaxHealth, _stats.currentHealth, 0.001f);
    }

    [Test]
    public void SubirDeNivelDaPuntosYRellenaLaVida()
    {
        _stats.currentHealth = 1f;
        _stats.AddExperience(_stats.ExperienceToNextLevel());
        Assert.AreEqual(2, _stats.level);
        Assert.AreEqual(_stats.pointsPerLevel, _stats.pointsAvailable);
        Assert.AreEqual(_stats.MaxHealth, _stats.currentHealth, 0.001f);
    }

    [Test]
    public void RepartirPuntosNoPasaDeLosDisponiblesYSubeLaVida()
    {
        _stats.AddExperience(_stats.ExperienceToNextLevel());
        float before = _stats.MaxHealth;
        int spent = _stats.AddPoints(StatType.Health, 99);
        Assert.AreEqual(_stats.pointsPerLevel, spent);
        Assert.AreEqual(0, _stats.pointsAvailable);
        Assert.AreEqual(before + spent * _stats.healthPerPoint, _stats.MaxHealth, 0.001f);
    }

    [Test]
    public void LaArmaduraReduceElDano()
    {
        _stats.SetEquipmentBonus(default, Mathf.RoundToInt(_stats.armorForHalfDamage), 0f);
        float before = _stats.currentHealth;
        _stats.TakeDamage(20f);
        Assert.AreEqual(10f, before - _stats.currentHealth, 0.01f);
    }

    [Test]
    public void MorirDisparaElEventoUnaSolaVez()
    {
        int deaths = 0;
        _stats.Died += () => deaths++;
        _stats.TakeDamage(_stats.MaxHealth * 10f);
        _stats.TakeDamage(_stats.MaxHealth * 10f);
        Assert.IsTrue(_stats.IsDead);
        Assert.AreEqual(1, deaths);
    }

    [Test]
    public void InvulnerableNoRecibeDanoYMuertoNoSeCura()
    {
        _stats.invulnerable = true;
        _stats.TakeDamage(50f);
        Assert.AreEqual(_stats.MaxHealth, _stats.currentHealth, 0.001f);
        _stats.invulnerable = false;
        _stats.TakeDamage(_stats.MaxHealth * 10f);
        _stats.Heal(50f);
        Assert.AreEqual(0f, _stats.currentHealth, 0.001f);
    }
}
