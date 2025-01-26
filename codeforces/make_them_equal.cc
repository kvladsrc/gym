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

// Minimal number of operations to get A from 1.
void min_cost(vector<int> &table) {
  int maximal = table.size() - 1;

  // {dist, v}
  queue<pair<int, int>> q;
  q.push({0, 1});

  while (!q.empty()) {
    auto cur = q.front();
    q.pop();

    if (cur.second > maximal) {
      continue;
    }

    if (table[cur.second] != -1) {
      continue;
    }

    table[cur.second] = cur.first;

    for (int i = 1; i <= cur.second; ++i) {
      auto n = cur.second + (cur.second / i);
      if (n <= maximal && table[n] == -1) {
        q.push({cur.first + 1, n});
      }
    }
  }
}

int backpack_rec(vector<int> &table, vector<int> &b, vector<int> &c, int k,
                 size_t start) {
  if (start >= b.size()) {
    return 0;
  }

  auto cost = table[b[start]];

  if (cost <= k) {
    auto case1 = c[start] + backpack_rec(table, b, c, k - cost, start + 1);
    auto case2 = backpack_rec(table, b, c, k, start + 1);
    return max(case1, case2);
  }

  return backpack_rec(table, b, c, k, start + 1);
}

bool compare_nums(const pair<int, int> &a, const pair<int, int> &b) {
  float aval = static_cast<float>(a.second) / static_cast<float>(a.first);
  float bval = static_cast<float>(b.second) / static_cast<float>(b.first);

  if (aval == bval) {
    return a.first > b.first;
  }

  if (aval < bval) {
    return true;
  }

  return false;
}

int backpack_greedy(vector<int> &table, vector<int> &b, vector<int> &c, int k) {
  vector<pair<int, int>> v(b.size());
  for (size_t idx = 0; idx < b.size(); ++idx) {
    v[idx] = {table[b[idx]], c[idx]};
  }

  sort(v.begin(), v.end(), compare_nums);

  int res = 0;
  while (!v.empty()) {
    auto cur = v.back();
    v.pop_back();

    if (cur.first <= k) {
      k -= cur.first;
      res += cur.second;
    }
  }

  return res;
}

int backpack(vector<int> &b, vector<int> &c, int k) {
  vector<vector<int>> dp(b.size() + 1, vector<int>(k + 1));
  for (int k_idx = 0; k_idx <= k; ++k_idx) {
    dp[0][k_idx] = 0;
  }

  for (int k_idx = 0; k_idx <= k; ++k_idx) {
    for (size_t item = 1; item <= b.size(); ++item) {
      if (b[item - 1] <= k_idx) {
        auto case1 = c[item - 1] + dp[item - 1][k_idx - b[item - 1]];
        auto case2 = dp[item - 1][k_idx];
        dp[item][k_idx] = max(case1, case2);
        continue;
      }

      dp[item][k_idx] = dp[item - 1][k_idx];
    }
  }

  return dp.back().back();
}

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;

  const int maximal = 1000;
  vector<int> table(maximal + 1, -1);
  min_cost(table);

  while ((t--) != 0) {
    size_t n = 0;
    int k = 0;
    std::cin >> n >> k;

    vector<int> b(n);
    for (auto &i : b) {
      int buf = 0;
      std::cin >> buf;
      i = table[buf];
    }

    vector<int> c(n);
    for (auto &i : c) {
      std::cin >> i;
    }

    // We have a two problems here. First -- minimal number of
    // operation to get b_i, let call the result vector costs. Then we
    // have a backpack problem here.

    // Had to read the hints. That is the great idea, was not able to
    // find it to pass the tests. So, 12n K is enought to take all
    // elements.
    k = min(k, 12 * static_cast<int>(n));

    auto res = backpack(b, c, k);
    std::cout << res << "\n";
  }

  return 0;
}
