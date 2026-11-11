# Installing the GameTranslator TV app

The TV apps are not in the stores yet. Turn on developer mode on your TV and GameTranslator installs the TV app by itself.
Developer mode is free and does not harm the TV. The desktop app shows these steps in its TV step and waits for the TV.

| TV | Status | Guide |
|---|---|---|
| **LG** (webOS 4+) | Tested on a real TV. GameTranslator installs the TV app itself | [LG](#lg-tv) |
| **Samsung** (Tizen, 2019 and later) | Tested on a real TV (75" Neo QLED). GameTranslator installs it; asks for a Samsung sign-in once | [Samsung](#samsung-tv) |
| **Android TV / Google TV** (Vestel, Arçelik, Beko, Grundig, Philips and others) | Tested on a real TV. GameTranslator installs it over the network | [Android TV](#android-tv--google-tv) |
| **Linux models of Vestel, Arçelik, Beko and Grundig, Philips Saphi and Titan OS** | Not supported: we found no way to install apps on them | |

The computer and the TV must be on the same home network.

## LG TV

1. Create a free account at [webostv.developer.lge.com](https://webostv.developer.lge.com) (**SIGN IN › Create Account**).
2. On the TV, install **Developer Mode** from the LG Content Store and sign in with that account.
3. Turn on **Dev Mode Status**. The TV restarts.
4. Open GameTranslator on your computer. In the TV step, pick your TV, press **Connect** and accept the prompt on the TV.
5. Open the Developer Mode app again, turn on **Key Server** and keep the app on screen.
6. Type the 6-character, case-sensitive **Passphrase** shown at the bottom left of the TV into GameTranslator and press **Install on the TV**.

GameTranslator installs later versions by itself and restarts the 1000-hour developer mode session each time you use it. If you
will not use GameTranslator for a long time, press **EXTEND** in the Developer Mode app before the time runs out: when it
expires, developer mode turns off and the TV app is removed.

## Samsung TV

Samsung requires apps to be signed for each TV. GameTranslator does this for you; you only sign in once with a free Samsung
account. Tested on a 75" Neo QLED.

1. In the TV's **Apps** screen press `1 2 3 4 5`, turn **Developer mode** on, enter your computer's address as
   **Host PC IP** (GameTranslator shows it) and restart the TV.
2. In the TV step of GameTranslator, pick your Samsung TV and press **Connect**.
3. GameTranslator opens Samsung's sign-in page in your browser. Sign in (or create a free account there). If the browser asks to
   allow access to devices on your network, allow it.
4. GameTranslator gets the certificate for your TV, signs the app, installs it and opens it.

If GameTranslator says the TV did not accept this computer, check that **Host PC IP** is your computer's current address and
restart the TV. Manual install with Tizen Studio: see the [Arabic guide](tv-setup.ar.md#manual-install).

## Android TV / Google TV

Tested on a real TV. Some TVs may not give the HDMI picture to other apps: subtitles appear but the picture stays
black. Please report your TV model in [Issues](https://github.com/mahmoude4477/GameTranslator/issues).

1. In **Settings › Device Preferences › About** (Google TV: **Settings › System › About**) press OK on **Build** 7 times.
2. In **Developer options** turn on **USB debugging** (and **Network debugging** if you see it).
3. In the TV step of GameTranslator, select **Android TV / Google TV**. GameTranslator finds the TV on the network.
4. The TV asks whether to allow debugging from this computer: tick **Always allow** and choose **Allow**. GameTranslator installs
   the app and opens it.
5. If there is no picture, press OK for the panel and pick the HDMI input with left and right.

If GameTranslator cannot find the TV, copy `tv/packages/gametranslator-androidtv_1.0.0.apk` from the repository (or
download it from [Releases](https://github.com/mahmoude4477/GameTranslator/releases) if a prebuilt release is published)
to a USB stick and install it with a file manager on the TV.

The prebuilt packages for all three platforms are in `tv/packages`; manual installation is described in the
[Arabic guide](tv-setup.ar.md#manual-install).
