using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class RagdollTests
{
    private GameObject _penguin;

    [SetUp]
    public void SetUp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Penguin_Rigged.prefab");
        _penguin = Object.Instantiate(prefab);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_penguin);

    [Test]
    public void ElRagdollSeActivaYSeDeshaceSinDejarRastro()
    {
        Transform head = _penguin.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
        Vector3 headPos = head.localPosition;
        Quaternion headRot = head.localRotation;
        int collidersBefore = _penguin.GetComponentsInChildren<Collider>(true).Length;

        var ragdoll = _penguin.AddComponent<PenguinRagdoll>();
        Assert.IsTrue(ragdoll.Activate(Vector3.forward, Vector3.up));
        Assert.IsTrue(ragdoll.IsActive);
        Assert.AreEqual(7, ragdoll.Bodies.Count);
        Assert.IsFalse(_penguin.GetComponentInChildren<PenguinRigAnimator>().enabled);

        head.localPosition += Vector3.one * 0.3f;
        ragdoll.Deactivate();

        Assert.IsFalse(ragdoll.IsActive);
        Assert.AreEqual(0, _penguin.GetComponentsInChildren<Rigidbody>(true).Length);
        Assert.AreEqual(0, _penguin.GetComponentsInChildren<Joint>(true).Length);
        Assert.AreEqual(collidersBefore, _penguin.GetComponentsInChildren<Collider>(true).Length);
        Assert.Less(Vector3.Distance(headPos, head.localPosition), 0.0001f);
        Assert.Less(Quaternion.Angle(headRot, head.localRotation), 0.01f);
        Assert.IsTrue(_penguin.GetComponentInChildren<PenguinRigAnimator>().enabled);
    }
}
