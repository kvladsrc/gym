#include <iostream>
#include <numeric>
#include <utility>
#include <vector>

namespace {

class DisjointSetUnion {
 public:
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

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  size_t n = 0;
  std::cin >> n;
  auto dsu = DisjointSetUnion(n);

  int ops = 0;
  std::cin >> ops;
  for (int i = 0; i < ops; ++i) {
    size_t a = 0;
    size_t b = 0;
    std::cin >> a >> b;
    dsu.unite(a, b);
  }

  int q = 0;
  std::cin >> q;
  for (int i = 0; i < q; ++i) {
    size_t a = 0;
    size_t b = 0;
    std::cin >> a >> b;

    if (dsu.connected(a, b)) {
      std::cout << "CONNECTED" << "\n";
    } else {
      std::cout << "DISCONNECTED" << "\n";
    }
  }

  return 0;
}
