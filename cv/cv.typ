#let blue = rgb("#4285F4")
#let red = rgb("#DB4437")
#let yellow = rgb("#F4B400")
#let green = rgb("#0F9D58")
#let ink = rgb("#202124")
#let muted = rgb("#5f6368")
#let line_color = rgb("#dadce0")
#let wash = rgb("#f8fafd")

#set page(paper: "a4", margin: (x: 1.35cm, y: 1.25cm))
#set text(size: 9.2pt, fill: ink)
#set par(justify: false, leading: 0.52em)
#show link: set text(fill: ink)

#let stripe() = grid(
  columns: (12pt, 3fr, 2fr, 2fr, 2fr),
  rect(fill: blue, width: 100%, height: 4.5pt)[
    #align(center + horizon)[#text(fill: rgb("#ffffff"), weight: "bold", size: 5pt)[>]]
  ],
  rect(fill: blue, width: 100%, height: 4.5pt),
  rect(fill: red, width: 100%, height: 4.5pt),
  rect(fill: yellow, width: 100%, height: 4.5pt),
  rect(fill: green, width: 100%, height: 4.5pt),
)

#let section(color, title, body) = block(below: 9pt)[
  #line(length: 100%, stroke: 0.45pt + line_color)
  #v(5pt)
  #box(circle(fill: color, radius: 3.5pt))
  #h(5pt)
  #text(size: 8.3pt, weight: "bold", tracking: 0.4pt, upper(title))
  #v(4pt)
  #body
]

#let job(period, role, company, points, technologies) = grid(
  columns: (3.1cm, 1fr),
  gutter: 10pt,
  [#text(size: 7.8pt, weight: "bold", fill: muted)[#period]],
  [
    #text(size: 10.2pt, weight: "bold")[#role]
    #text(fill: muted, weight: "semibold")[ at #company]
    #v(2pt)
    #for point in points [
      - #point
    ]
    #text(size: 7.8pt, fill: muted)[*Technologies:* #technologies]
  ],
)

#stripe()
#v(14pt)
#grid(
  columns: (1fr, 6.6cm),
  gutter: 18pt,
  [
    #text(size: 25pt, weight: "bold")[Vladimir Kim] \
    #text(size: 10pt, fill: muted, weight: "semibold")[Platform Engineer | Site Reliability Engineer | Backend Developer]
  ],
  [
    #block(inset: (left: 8pt), stroke: (left: 2pt + blue))[
      #text(size: 7.9pt)[#link("mailto:v.a.kim1988@gmail.com")[v.a.kim1988\@gmail.com]  #text(fill: muted)[|]  #link("tel:+48451633718")[+48 451 633 718]  #text(fill: muted)[|]  #link("https://linkedin.com/in/vladimir-kim-7b20a6223")[linkedin.com/in/vladimir-kim-7b20a6223]  #text(fill: muted)[|]  #link("https://github.com/kvladsrc")[github.com/kvladsrc]]
    ]
  ],
)

#section(blue, "Professional Summary")[
  Platform engineer with 10+ years of experience building and operating production infrastructure. Strong in Go services, Kubernetes and OpenShift platforms, CI/CD, observability, and cloud automation on GCP. Comfortable moving between backend development and SRE work: designing reliable systems, improving delivery pipelines, and keeping complex platforms understandable.
]

#section(red, "Target Positions")[
- Platform Engineer / Infrastructure Engineer
- Site Reliability Engineer (SRE)
- DevOps Engineer
- Backend Developer (Go)
]

#section(yellow, "Technical Skills")[
- Proficient in Go language and ecosystem; have experience with Python and C++.
- Experienced in Linux OS and operations: Bash, coreutils, Ansible, RHEL, NixOS, and Debian.
- Experienced in container-based infrastructure: OpenShift, Kubernetes, OCI containers, Docker, and cloud-native platform operations.
- Familiar with backend and API design: DRF, gRPC, and Protobuf.
- Familiar with CI/CD and GitOps practice: Jenkins, Zuul CI, ArgoCD, FluxCD, Bazel, Gerrit, and Cloud Build.
- Experienced in cloud automation and observability: GCP, Terraform, Prometheus/Thanos, Alertmanager, Grafana, ELK, and performance troubleshooting.
]

#section(green, "Work Experience")[
#job(
  "Dec 2022 - Present",
  "System Engineer",
  "EPAM Systems Poland",
  (
    "Develop Go microservices with a focus on reliability, maintainability, gRPC APIs, and test coverage.",
    "Manage build systems and cloud resources across large engineering projects.",
    "Improve infrastructure automation and developer workflows around Bazel, Gerrit, Terraform, Cloud Build, and GCP.",
  ),
  "Bazel, Gerrit, Terraform, Cloud Build, GCP, Go, Protobuf, gRPC",
)

#job(
  "May 2021 - Oct 2022",
  "DevOps Engineer",
  "Sberbank Kazakhstan, CIB",
  (
    "Operated Linux and OpenShift infrastructure for containerized services.",
    "Built observability flows across metrics, logs, dashboards, and alerting.",
    "Led GitOps and platform delivery initiatives using Helm, ArgoCD, Jenkins, and Ansible.",
  ),
  "OpenShift, Helm/Helmfile, ArgoCD, Jenkins, Ansible, Kafka, ELK, Prometheus/Thanos, Alertmanager, Grafana, RHEL",
)

#job(
  "Dec 2014 - May 2021",
  "Programmer / System Administrator",
  "Polpharma, SA",
  (
    "Built internal business applications and backend APIs with Python.",
    "Administered Linux servers, load balancing, monitoring, and containerized deployments.",
    "Maintained SAP ERP and Oracle database environments, including availability and performance work.",
    "Supported data processing pipelines using Hadoop and ETL tooling.",
  ),
  "Docker Swarm, Python, HAProxy, Nginx, Ansible, Hadoop, Oracle RDBMS, SAP ERP",
)

#job(
  "Nov 2012 - Oct 2014",
  "Developer",
  "openerp.kz, Llp",
  (
    "Developed backend services using Python web and asynchronous frameworks.",
    "Implemented embedded Linux integrations on Raspberry Pi with peripheral devices.",
  ),
  "Twisted, asyncio, C, Raspberry Pi, Linux",
)
]
