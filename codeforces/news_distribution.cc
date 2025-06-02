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
using std::gcd;
using std::map;
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

size_t find_parent(vector<int> &p, int el) {
  if (p[el] == el) return el;
  return find_parent(p, p[el]);
}

void join(vector<int> &rank, vector<int> &p, int a, int b) {
  auto p_a = find_parent(p, a);
  auto p_b = find_parent(p, b);

  if (p_a == p_b) {
    return;
  }

  if (rank[p_a] > rank[p_b]) {
    p[p_b] = p_a;
  } else if (rank[p_a] < rank[p_b]) {
    p[p_a] = p_b;
  } else {
    p[p_a] = p_b;
    rank[p_b]++;
  }
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, m;
  std::cin >> n >> m;

  // Let's use DSU. CC are also works.
  vector<int> parent(n);
  vector<int> rank(n, 0);
  for (size_t idx = 0; idx < parent.size(); ++idx) {
    parent[idx] = idx;
  }

  while (m--) {
    int num;
    std::cin >> num;

    int first;
    if (num) {
      std::cin >> first;
      first--;
      num--;
    }

    while (num--) {
      int el;
      std::cin >> el;
      el--;
      join(rank, parent, first, el);
    }
  }

  map<int, int> cc;
  for (size_t idx = 0; idx < parent.size(); ++idx) {
    auto p = find_parent(parent, idx);
    cc[p]++;
  }

  for (size_t idx = 0; idx < parent.size(); ++idx) {
    auto p = find_parent(parent, idx);
    std::cout << cc[p] << " ";
  }
  std::cout << "\n";

  return 0;
}
