#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const int modulo = 1000000007;

int64_t num_ways(int n, int k, int d, bool d_used,
                 vector<int64_t> &table_with_d,
                 vector<int64_t> &table_without_d) {
  if (n == 0) {
    return (d_used ? 1 : 0);
  }

  if (n < 0 || (!d_used && (n - d) < 0)) return 0;

  if (d_used && table_with_d[n] != -1) {
    return table_with_d[n];
  }

  if (!d_used && table_without_d[n] != -1) {
    return table_without_d[n];
  }

  int64_t res = 0;
  for (int i = 1; i <= k; ++i) {
    bool d_used_cur = d_used || i >= d;
    res += num_ways(n - i, k, d, d_used_cur, table_with_d, table_without_d);
    res %= modulo;
  }

  if (d_used) {
    table_with_d[n] = res;
  } else {
    table_without_d[n] = res;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int n, k, d;
  std::cin >> n >> k >> d;

  vector<int64_t> table_with_d(n + 1, -1);
  vector<int64_t> table_without_d(n + 1, -1);

  std::cout << num_ways(n, k, d, false, table_with_d, table_without_d) << "\n";

  return 0;
}
