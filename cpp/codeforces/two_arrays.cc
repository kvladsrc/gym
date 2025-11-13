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

const int64_t modulo = 1000000007;

void fill_table(size_t n, size_t m, vector<vector<int64_t>>& table) {
  std::fill(table[0].begin(), table[0].end(), 1);

  for (size_t i = 1; i < n; ++i) {
    table[i][0] = 0;
  }

  for (size_t i = 1; i < n; ++i) {
    for (size_t j = 1; j < m; ++j) {
      table[i][j] = (table[i - 1][j] + table[i][j - 1]) % modulo;
    }
  }
}

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  size_t n = 0;
  size_t m = 0;
  std::cin >> n >> m;

  vector<vector<int64_t>> table(n, vector<int64_t>(m));
  fill_table(n, m, table);

  vector<int64_t> fpref(n + 1);
  fpref.front() = 0;

  for (size_t i = 1; i <= n; ++i) {
    fpref[i] = fpref[i - 1];
    fpref[i] = (fpref[i] + table[i - 1][m - 1]) % modulo;
  }

  vector<int64_t> spref(n + 1);
  spref.front() = 0;

  for (size_t i = 1; i <= n; ++i) {
    spref[i] = spref[i - 1];
    spref[i] = (spref[i] + fpref[i]) % modulo;
  }

  int64_t res = 0;
  for (size_t i = 1; i <= n; ++i) {
    auto up = fpref[n - i + 1];
    auto down = spref[i];
    auto local = (up * down) % modulo;
    res = (res + local) % modulo;
  }

  std::cout << res << "\n";

  return 0;
}
