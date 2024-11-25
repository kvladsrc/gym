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
  int n;
  std::cin >> n;
  int xxx = 0;
  int res = 0;
  while (n--) {
    char c;
    std::cin >> c;
    if (c == 'x') {
      xxx++;
    } else {
      xxx = 0;
    }

    if (xxx == 3) {
      res++;
      xxx--;
    }
  }
  std::cout << res << "\n";

  return 0;
}
