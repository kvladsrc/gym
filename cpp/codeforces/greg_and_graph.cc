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

using adj_matrix = vector<vector<int>>;

int64_t add_v(const adj_matrix& g, vector<int>& available,
              vector<vector<int64_t>>& dists, int new_v, int64_t cur_sum) {
  // Init weights.
  for (auto a : available) {
    dists[a][new_v] = g[a][new_v];
    dists[new_v][a] = g[new_v][a];
    cur_sum += (dists[new_v][a] + dists[a][new_v]);
  }

  // Shortest pathes from any V to New_V.
  for (auto from : available) {
    for (auto by : available) {
      auto cur_dist = dists[from][new_v];
      auto new_dist = dists[from][by] + dists[by][new_v];
      if (cur_dist > new_dist) {
        dists[from][new_v] = new_dist;
        cur_sum -= cur_dist;
        cur_sum += new_dist;
      }
    }
  }

  // Shortest pathes from New_V to any V.
  for (auto to : available) {
    for (auto by : available) {
      auto cur_dist = dists[new_v][to];
      auto new_dist = dists[new_v][by] + dists[by][to];
      if (cur_dist > new_dist) {
        dists[new_v][to] = new_dist;
        cur_sum -= cur_dist;
        cur_sum += new_dist;
      }
    }
  }

  // Shortest pathes from any V_1 to any V_2 by New_V.
  for (auto from : available) {
    for (auto to : available) {
      auto cur_dist = dists[from][to];
      auto new_dist = dists[from][new_v] + dists[new_v][to];
      if (cur_dist > new_dist) {
        dists[from][to] = new_dist;
        cur_sum -= cur_dist;
        cur_sum += new_dist;
      }
    }
  }

  available.push_back(new_v);
  return cur_sum;
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  std::cin >> n;

  adj_matrix g(n, vector<int>(n));
  for (size_t y = 0; y < n; ++y) {
    for (size_t x = 0; x < n; ++x) {
      std::cin >> g[y][x];
    }
  }

  vector<int> remove(n);
  for (auto& i : remove) {
    std::cin >> i;
  }

  vector<int> available;
  vector<int64_t> res(n + 1);
  res.front() = 0;
  std::reverse(remove.begin(), remove.end());
  vector<vector<int64_t>> dists(n, vector<int64_t>(n, INT64_MAX));

  for (size_t idx = 0; idx < n; ++idx) {
    res[idx + 1] = add_v(g, available, dists, remove[idx] - 1, res[idx]);
  }

  std::reverse(res.begin(), res.end());
  res.pop_back();
  for (auto r : res) {
    std::cout << r << " ";
  }
  std::cout << "\n";

  return 0;
}
