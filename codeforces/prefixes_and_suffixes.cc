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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
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

void calculate_prefix_suffix(const string &s, vector<int> &table) {
  table.resize(s.size());
  table.front() = 0;

  int k = 0;

  for (size_t idx = 1; idx < s.size(); ++idx) {
    while (k > 0 && s[idx] != s[k]) {
      k = table[k - 1];
    }

    if (s[idx] == s[k]) {
      ++k;
    }

    table[idx] = k;
  }
}

using graph = vector<set<int>>;

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  vector<int> table;
  calculate_prefix_suffix(s, table);

  graph g(s.size() + 1);
  g[table.back()].insert(s.size());
  map<int, int> m;
  m[s.size()] = 1;

  for (auto i : table) {
    m[i]++;
    if (i != 0) {
      auto p = table[i - 1];
      g[p].insert(i);
    }
  }

  for (int i = 0; i < static_cast<int>(s.size()); ++i) {
    for (auto n : g[s.size() - i]) {
      m[s.size() - i] += m[n];
    }
  }

  vector<pair<int, int>> res;
  int k = s.size();

  while (k != 0) {
    res.push_back({k, m[k]});
    k = table[k - 1];
  }

  std::cout << res.size() << "\n";
  std::sort(res.begin(), res.end());
  for (auto p : res) {
    std::cout << p.first << " " << p.second << "\n";
  }

  return 0;
}
