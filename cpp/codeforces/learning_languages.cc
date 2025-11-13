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

class dsu {
  vector<size_t> parent;
  vector<int> rank;

 public:
  size_t find_parent(size_t a) {
    if (parent[a] == a) {
      return a;
    }
    return find_parent(parent[a]);
  }

  dsu() {}
  explicit dsu(size_t s) {
    parent.resize(s);
    rank.resize(s);
    for (size_t idx = 0; idx < s; ++idx) {
      parent[idx] = idx;
      rank[idx] = 0;
    }
  }

  bool connected(size_t a, size_t b) {
    auto p_a = find_parent(a);
    auto p_b = find_parent(b);
    return p_a == p_b;
  }

  void join(size_t a, size_t b) {
    if (connected(a, b)) {
      return;
    }
    auto p_a = find_parent(a);
    auto p_b = find_parent(b);
    if (rank[p_a] > rank[p_b]) {
      parent[p_b] = p_a;
    } else if (rank[p_a] < rank[p_b]) {
      parent[p_a] = p_b;
    } else {
      parent[p_a] = p_b;
      rank[p_b]++;
    }
  }

  int cc(const map<int, bool> &used_language) {
    int res = 0;
    for (size_t idx = 0; idx < parent.size(); ++idx) {
      auto it = used_language.find(idx);
      if (it == used_language.end() || !it->second) continue;
      if (parent[idx] == idx) {
        res++;
      }
    }
    return res;
  }
};

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, m;
  std::cin >> n >> m;

  auto d = dsu(m);
  map<int, bool> used_language;
  int zero = 0;

  while (n--) {
    int langs;
    std::cin >> langs;
    if (!langs) zero++;
    vector<int> l(langs);
    for (auto &i : l) {
      std::cin >> i;
      used_language[i - 1] = true;
    }
    for (auto i : l) {
      d.join(i - 1, l.front() - 1);
    }
  }

  int res = 0;
  map<int, bool> added_p;
  for (int lang = 0; lang < m; ++lang) {
    if (!used_language[lang]) {
      continue;
    }

    auto p_lang = d.find_parent(lang);
    if (added_p[p_lang]) {
      continue;
    }

    added_p[p_lang] = true;
    res++;
  }

  if (res) {
    res--;
  }

  res += zero;
  std::cout << res << "\n";

  return 0;
}
