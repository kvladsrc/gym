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

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;

  int extra_ones = m - n;

  if (extra_ones < -1) {
    std::cout << -1 << "\n";
    return 0;
  }

  if (extra_ones > (n + 2)) {
    std::cout << -1 << "\n";
    return 0;
  }

  string res;
  while (n > 0) {
    if (extra_ones > 0) {
      res += "011";
      extra_ones--;
      m -= 2;
      n -= 1;
      continue;
    }

    res += "01";
    m--;
    n--;
  }

  if (extra_ones == 1) {
    res = "1" + res;
  } else if (extra_ones == 2) {
    res = "11" + res;
  } else if (extra_ones == -1) {
    res.pop_back();
  }
  std::cout << res << "\n";

  return 0;
}
