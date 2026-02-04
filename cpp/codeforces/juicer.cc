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
  int n, b, d;
  std::cin >> n >> b >> d;
  int waste = 0;
  int res = 0;
  while (n--) {
    int orange;
    std::cin >> orange;

    if (orange > b) continue;

    waste += orange;
    if (waste > d) {
      waste = 0;
      res++;
    }
  }

  std::cout << res << "\n";

  return 0;
}
