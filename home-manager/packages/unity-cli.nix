{ pkgs }:
pkgs.stdenv.mkDerivation {
  pname = "unity-cli";
  version = "1.0.0-beta.9";
  src = pkgs.fetchurl {
    url = "https://public-cdn.cloud.unity3d.com/hub/prod/cli/1.0.0-beta.9/unity-linux-x64";
    sha256 = "8c0d6e2435449c8be7f0e6b2ce6330bfc5f17a98aec4b659c859940455dd0fe5"; # pragma: allowlist secret
  };
  dontUnpack = true;
  nativeBuildInputs = [ pkgs.autoPatchelfHook pkgs.makeWrapper ];
  buildInputs = [ pkgs.stdenv.cc.cc.lib ];
  installPhase = ''
    install -Dm755 "$src" "$out/bin/unity"
  '';
  postFixup = ''
    wrapProgram "$out/bin/unity" \
      --prefix LD_LIBRARY_PATH : ${pkgs.lib.makeLibraryPath [ pkgs.openssl pkgs.icu pkgs.zlib pkgs.stdenv.cc.cc.lib ]}
  '';
}
