# Presentations

Presentations are written in LaTeX Beamer and built with the same `latexmk`
and LuaLaTeX toolchain as the repository CV.

Build the mock backpropagation deck from the repository root:

```sh
just demo::backprop
```

The command writes the presentation to `demo/build/backprop.pdf`. Edit
`backprop/backprop.tex` to change its slides. The deck uses Beamer's built-in
theme and does not require a separate style file.
