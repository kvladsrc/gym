#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
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
    int a, b, c;
    std::cin >> a >> b >> c;
    if (a == b) std::cout << c << "\n";
    if (a == c) std::cout << b << "\n";
    if (c == b) std::cout << a << "\n";
  }
  return 0;
}
