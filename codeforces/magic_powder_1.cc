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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int n, k;
  std::cin >> n >> k;
  vector<int> a(n), b(n);
  for (auto &i : a) {
    std::cin >> i;
  }
  for (auto &i : b) {
    std::cin >> i;
  }

  int cookies = INT_MAX;
  for (int i = 0; i < n; ++i) {
    auto can_be = b[i] / a[i];
    if (can_be < cookies) {
      cookies = can_be;
    }
  }
  for (int i = 0; i < n; ++i) {
    b[i] -= a[i] * cookies;
  }

  while (k) {
    bool cooked = true;
    for (int i = 0; i < n; ++i) {
      if (b[i] >= a[i]) {
        b[i] -= a[i];
        continue;
      }

      if (k >= (a[i] - b[i])) {
        k -= a[i] - b[i];
        b[i] = 0;
        continue;
      }

      cooked = false;
      break;
    }

    if (cooked) {
      cookies++;
    } else {
      break;
    }
  }

  std::cout << cookies << "\n";

  return 0;
}
