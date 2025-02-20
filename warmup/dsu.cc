#include <iostream>
#include <vector>

using std::vector;

namespace {

const int default_size = 100;

class DisjointSetUnion {
 public:
  DisjointSetUnion() : DisjointSetUnion(default_size) {}
  explicit DisjointSetUnion(int /*size*/);
  bool Connected(int /*a*/, int /*b*/);
  void Unite(int /*a*/, int /*b*/);

 private:
  vector<int> parent;
  vector<int> rank;
  int find(int /*i*/);
};

DisjointSetUnion::DisjointSetUnion(int size) {
  parent.resize(size);
  rank.resize(size, 0);
  for (int i = 0; i < size; ++i) {
    parent[i] = i;
  }
}

int DisjointSetUnion::find(int i) {
  // Path compression.
  if (parent[i] != i) {
    parent[i] = find(parent[i]);
  }
  return parent[i];
}

bool DisjointSetUnion::Connected(int a, int b) { return find(a) == find(b); }

void DisjointSetUnion::Unite(int a, int b) {
  auto pa = find(a);
  auto pb = find(b);

  if (pa == pb) {
    return;
  }

  if (rank[pa] == rank[pb]) {
    parent[pa] = pb;
    rank[pb]++;
    return;
  }

  auto mx = (rank[pa] > rank[pb] ? pa : pb);
  auto mn = (rank[pa] > rank[pb] ? pb : pa);
  parent[mn] = mx;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  auto dsu = DisjointSetUnion(n);

  int ops = 0;
  std::cin >> ops;
  for (int i = 0; i < ops; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    dsu.Unite(a, b);
  }

  int q = 0;
  std::cin >> q;
  for (int i = 0; i < q; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;

    if (dsu.Connected(a, b)) {
      std::cout << "CONNECTED" << "\n";
    } else {
      std::cout << "DISCONNECTED" << "\n";
    }
  }

  return 0;
}
