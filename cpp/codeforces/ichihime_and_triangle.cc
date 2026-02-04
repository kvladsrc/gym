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
    int a, b, c, d;
    std::cin >> a >> b >> c >> d;

    for (int z = c; z <= d; ++z) {
      for (int y = c; y >= b; --y) {
        for (int x = b; x >= a; --x) {
          if (z >= y + x) {
            continue;
          } else {
            std::cout << x << " ";
            std::cout << y << " ";
            std::cout << z << "\n";

            goto end;
          }
        }
      }
    }
  end: {}
  }
  return 0;
}
