#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <pthread.h>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const int default_size = 100;

class DisjointSetUnion {
public:
  DisjointSetUnion() : DisjointSetUnion(default_size) {}
  DisjointSetUnion(int);
  bool Connected(int, int);
  void Unite(int, int);

private:
  vector<int> parent;
  vector<int> rank;
  int find_parent(int);
};

DisjointSetUnion::DisjointSetUnion(int size) {
  parent.resize(size);
  rank.resize(size, 0);
  for (int i = 0; i < size; ++i) {
    parent[i] = i;
  }
}

int DisjointSetUnion::find_parent(int i) {
  if (parent[i] == i)
    return i;
  return find_parent(parent[i]);
}

bool DisjointSetUnion::Connected(int a, int b) {
  return find_parent(a) == find_parent(b);
}

void DisjointSetUnion::Unite(int a, int b) {
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

int main(int argc, char *argv[]) {
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
