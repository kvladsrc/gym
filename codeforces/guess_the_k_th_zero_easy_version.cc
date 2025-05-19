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

int ask(int x, int y) {
  std::cout << "? " << x << " " << y << "\n";
  int res = 0;
  std::cin >> res;
  return res;
}

void ans(int a) {
  std::cout << "! " << a << "\n";
  std::cout.flush();
}

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  int t = 0;
  int k = 0;
  std::cin >> n >> t >> k;

  int l = 1;
  int r = n;
  int res = -1;
  while (l <= r) {
    int m = (l + r) / 2;
    auto zeroes = m - ask(1, m);
    if (zeroes >= k) {
      res = m;
      r = m - 1;
    } else {
      l = m + 1;
    }
  }

  ans(res);

  return 0;
}
