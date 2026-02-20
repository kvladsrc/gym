#!/bin/bash
# Toggle between dark and light GTK color scheme.
# Affects browsers and other GTK apps.

CURRENT=$(gsettings get org.gnome.desktop.interface color-scheme)

if [[ $CURRENT == "'prefer-dark'" ]]; then
    gsettings set org.gnome.desktop.interface color-scheme "prefer-light"
    gsettings set org.gnome.desktop.interface gtk-theme "adw-gtk3"
else
    gsettings set org.gnome.desktop.interface color-scheme "prefer-dark"
    gsettings set org.gnome.desktop.interface gtk-theme "adw-gtk3-dark"
fi
