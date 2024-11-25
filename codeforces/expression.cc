#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int max_expr(int a, int b, int c) {
  int max = 0;
  vector<int> cases = {a + b + c, a * b * c, (a + b) * c, a * (b + c)};

  for (int v : cases) {
    if (v > max) max = v;
  }

  return max;
}

int main(int argc, char *argv[]) {
  int a, b, c;
  std::cin >> a >> b >> c;

  std::cout << max_expr(a, b, c) << "\n";

  return 0;
}
