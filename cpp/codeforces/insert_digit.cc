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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, d;
    std::cin >> n >> d;

    string num;
    std::cin >> num;

    string res;
    bool added = false;
    for (char c : num) {
      if (added) {
        res += c;
        continue;
      }

      if (d > (c - '0')) {
        res += '0' + d;
        res += c;
        added = true;
      } else {
        res += c;
      }
    }

    if (!added) {
      res += '0' + d;
    }

    std::cout << res << "\n";
  }

  return 0;
}
