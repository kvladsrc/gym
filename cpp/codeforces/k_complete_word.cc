#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
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
    size_t k = 0;
    std::cin >> n >> k;

    string s;
    std::cin >> s;

    DisjointSet dsu(n);

    for (size_t idx = 0; idx < k; ++idx) {
      for (size_t idx_2 = idx; idx_2 < n; idx_2 += k) {
        dsu.join(idx_2, idx);
        dsu.join((n - 1) - idx_2, idx);
      }
    }

    map<size_t, int> ssize;
    map<size_t, map<char, int>> schars;

    for (size_t idx = 0; idx < n; ++idx) {
      auto par = dsu.find_parent(idx);
      ssize[par]++;
      schars[par][s[idx]]++;
    }

    int res = 0;
    for (auto p : ssize) {
      int most_freq = std::accumulate(
          schars[p.first].begin(), schars[p.first].end(), 0,
          [](int acc, const auto& pc) { return max(acc, pc.second); });
      res += p.second - most_freq;
    }

    std::cout << res << "\n";
  }

  return 0;
}
