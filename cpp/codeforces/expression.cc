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
  int max_val = 0;
  vector<int> cases = {a + b + c, a * b * c, (a + b) * c, a * (b + c)};

  max_val = *std::max_element(cases.begin(), cases.end());

  return max_val;
}

int main(int argc, char* argv[]) {
  int a, b, c;
  std::cin >> a >> b >> c;

  std::cout << max_expr(a, b, c) << "\n";

  return 0;
}
