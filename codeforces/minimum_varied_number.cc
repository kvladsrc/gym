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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int cur = 9;
    string res;

    while (n) {
      res += min(n, cur) + '0';
      n -= min(n, cur);
      cur--;
    }

    std::reverse(res.begin(), res.end());
    std::cout << res << "\n";
  }

  return 0;
}
