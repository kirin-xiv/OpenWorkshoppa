using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Workshoppa.GameData;

public sealed class IconCache : IDisposable
{
	private readonly ITextureProvider _textureProvider;

	private readonly Dictionary<uint, ISharedImmediateTexture> _textureWraps = new Dictionary<uint, ISharedImmediateTexture>();

	public IconCache(ITextureProvider textureProvider)
	{
		_textureProvider = textureProvider;
	}

	public IDalamudTextureWrap? GetIcon(uint iconId)
	{
		if (!_textureWraps.TryGetValue(iconId, out ISharedImmediateTexture iconTex))
		{
			iconTex = _textureProvider.GetFromGameIcon(new GameIconLookup(iconId));
			_textureWraps[iconId] = iconTex;
		}
		if (!iconTex.TryGetWrap(out IDalamudTextureWrap wrap, out Exception _))
		{
			return null;
		}
		return wrap;
	}

	public void Dispose()
	{
		_textureWraps.Clear();
	}
}
