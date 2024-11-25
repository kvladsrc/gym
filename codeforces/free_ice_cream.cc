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
  int64_t n, x;
  std::cin >> n >> x;

  int64_t distress = 0;
  while (n--) {
    char c;
    int64_t buf;
    std::cin >> c >> buf;

    if (c == '+') {
      x += buf;
    } else {
      if (buf > x) {
        distress++;
      } else {
        x -= buf;
      }
    }
  }

  std::cout << x << ' ' << distress << "\n";

  return 0;
}
