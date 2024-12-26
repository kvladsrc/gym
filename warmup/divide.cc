#include <iostream>
#include <string>

using std::pair;
using std::string;

static pair<int, int> divide(int a, int b) {
  if (a < b) {
    return {0, a};
  }

  auto buf = divide(a / 2, b);
  buf.first *= 2;
  buf.second *= 2;
  buf.second += a % 2;
  if (buf.second >= b) {
    buf.second -= b;
    buf.first++;
  }
  return buf;
}

int main(int /*argc*/, char* /*argv*/[]) {
  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    int a = 0;
    int b = 0;
    std::cin >> a >> b;
    auto res = divide(a, b);
    if ((res.first != a / b) || res.second != a % b) {
      std::cout << "Wrong!\n";
    } else {
      std::cout << "Ok!\n";
    }
  }

  return 0;
}
