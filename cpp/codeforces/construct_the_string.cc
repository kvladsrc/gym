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
    int n, a, b;
    std::cin >> n >> a >> b;
    char start = 'a';
    string res;
    int i = 0;
    while (n--) {
      res += start + i;
      i = (i + 1) % b;
    }
    std::cout << res << "\n";
  }
  return 0;
}
