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

// In reccurent form:
//
// F([a_1, a_2, ... a_n], l, r, min_used, max_used, num_used) := for
//   each i { F([a_j; j != i], l-a_i, r-a_i, min(min_used, a_i),
//   max(max_used, a_i), num_used+1) + F([a_j; j != i], l, r,
//   min_used, max_used, num_used)
//
// F([], l, r, min_used, max_used) := if l > 0 || r < 0 || max_used -
// min_used < x || num_used < 2 => 0 else 1
int solution_rec(vector<int> &problems, size_t start, int l, int r,
                 int min_used, int max_used, int x, int num_used) {
  if (r < 0) return 0;

  if (start == problems.size()) {
    if (l > 0 || r < 0 || (max_used - min_used) < x || num_used < 2) {
      return 0;
    } else {
      return 1;
    }
  }

  auto first = problems[start];
  auto res =
      solution_rec(problems, start + 1, l - first, r - first,
                   min(min_used, first), max(max_used, first), x, num_used + 1);
  res +=
      solution_rec(problems, start + 1, l, r, min_used, max_used, x, num_used);
  return res;
}

int main(int argc, char *argv[]) {
  int n, l, r, x;
  std::cin >> n >> l >> r >> x;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  // Naive solution: check each subset of A at O(2^n) time. Since n is
  // <= 15 we can use it. Because problem looks like a variation of
  // backpack problem we have a low chances to find fast solution.

  std::cout << solution_rec(a, 0, l, r, INT_MAX, INT_MIN, x, 0) << "\n";

  return 0;
}
