#include <iostream>
#include <string>
#include <vector>

using std::string;
using std::vector;

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
  int find_parent(int /*i*/);
};

DisjointSetUnion::DisjointSetUnion(int size) {
  parent.resize(size);
  rank.resize(size, 0);
  for (int i = 0; i < size; ++i) {
    parent[i] = i;
  }
}

int DisjointSetUnion::find_parent(int i) {
  if (parent[i] == i) {
    return i;
  }
  return find_parent(parent[i]);
}

bool DisjointSetUnion::Connected(int a, int b) {
  return find_parent(a) == find_parent(b);
}

void DisjointSetUnion::Unite(int a, int b) {
  if (Connected(a, b)) {
    return;
  }
  auto p_a = find_parent(a);
  auto p_b = find_parent(b);
  if (rank[p_a] < rank[p_b]) {
    parent[p_a] = p_b;
  } else if (rank[p_a] > rank[p_b]) {
    parent[p_b] = p_a;
  } else {
    parent[p_b] = p_a;
    rank[p_a]++;
  }
}

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
