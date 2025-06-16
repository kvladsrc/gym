#include <iostream>
#include <numeric>
#include <utility>
#include <vector>

using std::vector;

namespace dsu {

const size_t default_size = 100;

class DisjointSetUnion {
 public:
  DisjointSetUnion() : DisjointSetUnion(default_size) {}
  explicit DisjointSetUnion(size_t /*size*/);
  bool connected(size_t /*a*/, size_t /*b*/);
  void unite(size_t /*a*/, size_t /*b*/);

 private:
  std::vector<size_t> parent;
  std::vector<int> rank;
  size_t find(size_t /*i*/);
};

DisjointSetUnion::DisjointSetUnion(size_t size) {
  parent.resize(size);
  std::iota(parent.begin(), parent.end(), 0);
  rank.resize(size, 0);
}

size_t DisjointSetUnion::find(size_t i) {
  // Path compression.
  if (parent[i] != i) {
    parent[i] = find(parent[i]);
  }
  return parent[i];
}

bool DisjointSetUnion::connected(size_t a, size_t b) {
  return find(a) == find(b);
}

void DisjointSetUnion::unite(size_t a, size_t b) {
  auto pa = find(a);
  auto pb = find(b);

  if (pa == pb) {
    return;
  }

  if (rank[pa] < rank[pb]) {
    std::swap(pa, pb);
  }

  parent[pb] = pa;

  if (rank[pa] == rank[pb]) {
    ++rank[pa];
  }
}

}  // namespace dsu

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  int m = 0;
  std::cin >> m;

  dsu::DisjointSetUnion d(n);

  bool cycle_detected = false;

  while ((m--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;

    if (d.connected(a, b)) {
      cycle_detected = true;
    }

    d.unite(a, b);
  }

  std::cout << (cycle_detected ? "YES" : "NO") << "\n";

  return 0;
}
