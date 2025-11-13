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
  int n;
  std::cin >> n;

  int res = 0;
  for (int i = 1; n - i >= i; ++i) {
    if ((n - i) % i == 0) {
      res++;
    }
  }
  std::cout << res << "\n";

  return 0;
}
