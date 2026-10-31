# Research: procedural landscapes with rivers and vegetation

<!-- markdownlint-disable MD013 -->

Codex's literature report that started [DESIGN.md](DESIGN.md), kept as written.

Привет! Нашёл работы, которые подходят для **реалистичного игрового ландшафта с реками и растительностью**. Для первого прототипа я бы начал с **№1 — рельеф и реки, №4 — вода, №5 — размещение растений**.

Большинство ссылок ведёт на страницы авторов с PDF, видео или кодом.

1. **[Procedural Generation of Landscapes with Water Bodies Using Artificial Drainage Basins — Fischer et al., 2022](https://cgvr.cs.uni-bremen.de/papers/cgi22/TR.pdf)**
   Сначала генерируются речные бассейны, реки и озёра, затем вокруг них строится рельеф. Практичный вариант для игры: результат представлен картой высот, прототип реализован в Unity. В авторском тесте весь процесс для сетки 2048 × 2048 занимал менее 25 секунд — это ориентир для подготовки уровня или генерации при загрузке. [cgvr.cs.uni-bremen.de](https://cgvr.cs.uni-bremen.de/papers/cgi22/TR.pdf?utm_source=chatgpt.com)

2. **[Terrain Generation Using Procedural Models Based on Hydrology — Génevaux et al., 2013](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-01339224/)**
   Хорошая базовая статья: строит иерархическую речную сеть, определяет водосборы и типы рек, после чего формирует окружающий рельеф. Полезна для понимания того, как согласовать долины, притоки и горные хребты. Поддерживает управление генерацией через набросок и параметры. [ACM Transactions on Graphics](https://doi.org/10.1145/2461912.2461996?utm_source=chatgpt.com)

3. **[Large-scale Terrain Authoring through Interactive Erosion Simulation — Schott et al., 2023](https://aparis69.github.io/public_html/projects/schott2023_Uplift.html)**
   Генерирует горы и долины через сочетание поднятия поверхности и речной эрозии. Позволяет интерактивно задавать форму рельефа, сохраняя согласованный сток воды. Я бы рассматривал её для инструмента создания уровней, особенно горных. Есть **[авторский код](https://github.com/H-Schott/StreamPowerErosion)** со сборкой для Windows и Linux. [aparis69.github.io](https://aparis69.github.io/public_html/projects/schott2023_Uplift.html?utm_source=chatgpt.com)

4. **[Procedural Riverscapes — Peytavie et al., 2019](https://pubs.cs.uct.ac.za/id/eprint/1344/)**
   Особенно полезна для рек, к которым игрок подходит близко. Метод прокладывает русло по карте высот, формирует его ширину, глубину и профиль, затем создаёт согласованную анимацию поверхности воды в реальном времени. Рассматривает спокойные участки, пороги, пену и объекты, переносимые течением. [UCT Computer Science Research Document Archive](https://pubs.cs.uct.ac.za/id/eprint/1344/?utm_source=chatgpt.com)

5. **[Ecologically Sound Procedural Generation of Natural Environments — Onrust et al., 2017](https://onlinelibrary.wiley.com/doi/10.1155/2017/7057141)**
   Про размещение растений: какие виды занимают участок, с какой плотностью и насколько выраженными группами. Использует экологические карты, Poisson disk sampling и Wang tiles; для отображения — LOD и инстансинг. **Ограничение:** авторы проверяли метод преимущественно на травянистой растительности; качество для лесов отдельно не подтверждено. [Wiley Online Library](https://onlinelibrary.wiley.com/doi/10.1155/2017/7057141?utm_source=chatgpt.com)

6. **[Data-driven Authoring of Large-scale Ecosystems — Kapp et al., 2020](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-03007259/)**
   Более подходящая работа для подробного лесного уровня: отдельно формирует верхний ярус деревьев и подлесок. Использует реальные данные, машинное обучение и статистику экосистем. Есть **[код EcoLearn](https://github.com/jgain/EcoLearn)**. По составу метода это более сложный вариант реализации, чем размещение по картам плотности. [Adrien Peytavie](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-03007259/?utm_source=chatgpt.com)

7. **[Authoring Landscapes by Combining Ecosystem and Terrain Erosion Simulation — Cordonnier et al., 2017](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-01518967/)**
   Наиболее близка к задаче согласованного природного окружения: моделирует взаимное влияние растительности и эрозии, учитывая камень, песок, перегной, траву, кустарники и деревья. Полезна как основа архитектуры генератора, где растения зависят от состояния поверхности и сами изменяют её. [Adrien Peytavie](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-01518967/?utm_source=chatgpt.com)

8. **[DeadWood: Including Disturbance and Decay in the Depiction of Digital Nature — Peytavie et al., 2024](https://pubs.cs.uct.ac.za/id/eprint/1689/)**
   Для дополнительной достоверности леса: сухостой, поваленные стволы, повреждения и разложение. Авторы проверяли восприятие сцен и обнаружили, что наличие мёртвой древесины повышает их правдоподобие. Есть **[авторская реализация](https://github.com/oargudo/deadwood)**. [UCT Computer Science Research Document Archive](https://pubs.cs.uct.ac.za/id/eprint/1689/?utm_source=chatgpt.com)

Для твоей игры я бы предложил такую последовательность реализации — это моя инженерная рекомендация по этим работам:

| Этап | Что получать |
| --- | --- |
| Реки и рельеф | Общую речную сеть, озёра, долины и карту высот по №1 |
| Детали поверхности | Локальную эрозию, материалы и береговые зоны |
| Растения | Карты пригодности видов по влажности, уклону, высоте и освещению; затем размещение группами |
| Вода и отображение | Геометрию русла и течение по №4; инстансинг и LOD растительности |

**Если мир должен генерироваться чанками во время движения игрока**, дополнительно посмотри **[Dendry: A Procedural Model for Dendritic Patterns, 2019](https://github.com/mgaillard/Noise)**. Метод допускает локальное вычисление рельефа с согласованными речными структурами. При этом авторская CPU-реализация приводится с временем около 10 секунд на 512 × 512, поэтому локальная вычислимость сама по себе ещё не обеспечивает нужную игре скорость. [GitHub](https://github.com/mgaillard/Noise?utm_source=chatgpt.com)
