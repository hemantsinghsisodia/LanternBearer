using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class GraphicsDefaultsTests
{
    [Test]
    public void WebDefaultsToHigh()
    {
        Assert.AreEqual(GraphicsDefaults.HighLevel, GraphicsDefaults.For(RuntimePlatform.WebGLPlayer));
        Assert.AreEqual(2, GraphicsDefaults.For(RuntimePlatform.WebGLPlayer));
    }

    [TestCase(RuntimePlatform.WindowsPlayer)]
    [TestCase(RuntimePlatform.WindowsEditor)]
    [TestCase(RuntimePlatform.OSXPlayer)]
    [TestCase(RuntimePlatform.LinuxPlayer)]
    [TestCase(RuntimePlatform.Android)]
    public void NonWebKeepsMedium(RuntimePlatform platform)
    {
        Assert.AreEqual(GraphicsDefaults.MediumLevel, GraphicsDefaults.For(platform));
        Assert.AreEqual(1, GraphicsDefaults.For(platform));
    }

    // Forward+ lit shaders crash the browser GPU process on WebGL (black canvas, lost context); the web renderer must stay Forward.
    [Test]
    public void WebRendererUsesForwardNotForwardPlus()
    {
        Object renderer = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Web_Renderer.asset");
        Assert.IsNotNull(renderer);
        SerializedProperty mode = new SerializedObject(renderer).FindProperty("m_RenderingMode");
        Assert.IsNotNull(mode);
        Assert.AreEqual(0, mode.intValue, "Web_Renderer rendering mode must be Forward (0)");
    }

    [Test]
    public void UltraIsNeverTheDefault()
    {
        foreach (RuntimePlatform platform in System.Enum.GetValues(typeof(RuntimePlatform)))
        {
            Assert.Less(GraphicsDefaults.For(platform), 3, platform.ToString());
        }
    }

    [Test]
    public void QuitAndDisplayModeOptionsAreDesktopOnly()
    {
        Assert.IsFalse(GraphicsDefaults.CanQuit(RuntimePlatform.WebGLPlayer));
        Assert.IsFalse(GraphicsDefaults.HasDisplayModeOptions(RuntimePlatform.WebGLPlayer));
        Assert.IsTrue(GraphicsDefaults.CanQuit(RuntimePlatform.WindowsPlayer));
        Assert.IsTrue(GraphicsDefaults.HasDisplayModeOptions(RuntimePlatform.WindowsPlayer));
        Assert.IsTrue(GraphicsDefaults.IsWeb(RuntimePlatform.WebGLPlayer));
        Assert.IsFalse(GraphicsDefaults.IsWeb(RuntimePlatform.WindowsEditor));
    }
}
}
