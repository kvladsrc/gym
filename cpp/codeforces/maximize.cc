#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int x;
    std::cin >> x;

    int res = 0;
    for (int i = 2; i * i <= x; ++i) {
      if (x % i == 0) {
        res = x - i;
        break;
      }
    }

    if (!res) res = x - 1;

    std::cout << res << "\n";
  }

  return 0;
}
