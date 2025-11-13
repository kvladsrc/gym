#include <climits>
#include <cmath>
#include <cstddef>
#include <ctime>
#include <iostream>
#include <map>
#include <string>
#include <vector>

using std::map;
using std::string;
using std::vector;

const size_t default_size = 100;

class DisjointSet {
  vector<int> rank;
  vector<size_t> parent;

 public:
  DisjointSet() : DisjointSet(default_size) {}
  explicit DisjointSet(size_t size) : rank(size, 0), parent(size) {
    for (size_t idx = 0; idx < size; ++idx) {
      parent[idx] = idx;
    }
  }

  size_t find_parent(size_t el) {
    if (parent[el] == el) {
      return el;
    }
    return find_parent(parent[el]);
  }

  void join(size_t a, size_t b) {
    auto pa = find_parent(a);
    auto pb = find_parent(b);

    if (pa == pb) {
      return;
    }

    if (rank[pa] > rank[pb]) {
      parent[pb] = pa;
    } else if (rank[pb] > rank[pa]) {
      parent[pa] = pb;
    } else {
      parent[pa] = pb;
      rank[pb]++;
    }
  }
};

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    // After applying the permutation multiple times we got multiple
    // circles. The answer for each element is the number of elements
    // in it circle. Lets use DSU to find the circles and then map for
    // find the number of elements.

    DisjointSet dsu(n);

    for (size_t i = 0; i < n; ++i) {
      size_t buf;
      std::cin >> buf;
      buf--;
      dsu.join(buf, i);
    }

    map<size_t, int> m;
    for (size_t i = 0; i < n; ++i) {
      auto pi = dsu.find_parent(i);
      m[pi]++;
    }

    for (size_t i = 0; i < n; ++i) {
      auto pi = dsu.find_parent(i);
      std::cout << m[pi] << " ";
    }
    std::cout << "\n";
  }

  return 0;
}
