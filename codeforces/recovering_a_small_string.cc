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

    int f, s, t;
    t = min(26, n - 2);
    n -= t;
    s = min(26, n - 1);
    n -= s;
    f = n;

    string res;
    res += 'a' + f - 1;
    res += 'a' + s - 1;
    res += 'a' + t - 1;

    std::cout << res << "\n";
  }

  return 0;
}
