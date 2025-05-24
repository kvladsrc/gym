#include <algorithm>
#include <climits>
#include <cmath>
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

class dsu {
 private:
  vector<int> parent;
  vector<int> rank;

  int find_parent(int i) {
    if (parent[i] != i) {
      return find_parent(parent[i]);
    }
    return i;
  };

 public:
  dsu() = default;

  dsu(int size) : parent(size), rank(size, 0) {
    for (int i = 0; i < size; ++i) {
      parent[i] = i;
    }
  }

  bool connected(int a, int b) { return find_parent(a) == find_parent(b); }

  void join(int a, int b) {
    auto pa = find_parent(a);
    auto pb = find_parent(b);

    if (pa == pb) {
      return;
    }

    if (rank[pa] > rank[pb]) {
      parent[pb] = pa;
    } else if (rank[pa] == rank[pb]) {
      parent[pb] = pa;
      rank[pa]++;
    } else {
      parent[pa] = pb;
    }
  }
};

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n = 0;
  std::cin >> n;

  vector<pair<int, int>> edges;
  vector<pair<int, int>> extra_edges;
  auto d = dsu(n);

  for (int i = 1; i < n; ++i) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    a--;
    b--;
    edges.push_back({a, b});
    if (d.connected(a, b)) {
      extra_edges.push_back({a, b});
    }
    d.join(a, b);
  }

  std::cout << extra_edges.size() << "\n";
  for (int i = 1; i < n; ++i) {
    if (!d.connected(i, i - 1)) {
      std::cout << extra_edges.back().first + 1 << " "
                << extra_edges.back().second + 1 << " ";
      std::cout << i + 1 << " " << i << "\n";

      extra_edges.pop_back();
      d.join(i, i - 1);
    }
  }

  return 0;
}
