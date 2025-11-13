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

int ask(int l, int r) {
  std::cout << "? " << l << " " << r << "\n";
  int res = 0;
  std::cin >> res;

  return res;
}

void ans(int l) {
  std::cout << "! " << l << "\n";
  std::cout.flush();
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  bool in_left = true;
  auto all = ask(1, n);
  if (n == 2) {
    if (all == 2) {
      ans(1);
    } else {
      ans(2);
    }
    return 0;
  }

  if (all == 1) {
    in_left = false;
  } else if (all == n) {
    in_left = true;
  } else {
    auto lq = ask(1, all);
    if (lq == all) {
      in_left = true;
    } else {
      in_left = false;
    }
  }

  int res = -1;
  if (in_left) {
    auto l = 1;
    auto r = n;
    while (l <= r) {
      auto m = (l + r) / 2;
      if (m == n) {
        break;
      }
      auto q = ask(m, n);
      if (q != all) {
        r = m - 1;
      } else {
        res = m;
        l = m + 1;
      }
    }
  } else {
    auto l = 1;
    auto r = n;
    while (l <= r) {
      auto m = (l + r) / 2;
      if (m == 1) {
        break;
      }
      auto q = ask(1, m);
      if (q != all) {
        l = m + 1;
      } else {
        res = m;
        r = m - 1;
      }
    }
  }

  ans(res);

  return 0;
}
