#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int solve_dp(vector<int> &a) {
  vector<int> table(a.size());
  int max_points = 0;

  for (int i = a.size() - 1; i >= 0; --i) {
    if (a[i] + i >= static_cast<int>(a.size())) {
      table[i] = a[i];
    } else {
      table[i] = a[i] + table[i + a[i]];
    }

    if (table[i] > max_points) max_points = table[i];
  }

  return max_points;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
    }

    std::cout << solve_dp(a) << "\n";
  }

  return 0;
}
