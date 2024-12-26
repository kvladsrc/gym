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

// TODO(zjgkkn): Tests cases to bats file.
int main(int /*argc*/, char* /*argv*/[]) {
  auto dsu = DisjointSetUnion(10);

  if (dsu.Connected(1, 2)) {
    std::cout << "!ok\n";
  }

  dsu.Unite(1, 2);
  if (!dsu.Connected(1, 2)) {
    std::cout << "!ok\n";
  }

  std::cout << "All tests are done!\n";

  return 0;
}
